using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DWDN_Script
{
    /// <summary>
    /// Тактическая аура Знаменосца («Воодушевление Знамени» / Bannerman Aura Behavior).
    /// Превращает знаменосца из простого пехотинца без щита в ключевого командного баффера строя,
    /// сохраняя строгий баланс и не допуская стакинга нескольких знамён.
    ///
    /// Параметры баланса:
    /// - Условие работы: Знаменосец жив и держит знамя непосредственно в руке (Offhand или Primary).
    /// - Радиус ауры: 12.0 метров вокруг знаменосца (не стакается от нескольких знаменосцев).
    /// - Бонус союзникам в радиусе 12м:
    ///     1. +4 к броне всех зон (Голова, Корпус, Перчатки, Поножи).
    ///     2. +5% к скорости марша (MaxSpeedMultiplier) и +6% к скорости шага в бою (CombatMaxSpeedMultiplier).
    ///     3. +4% к скорости удара/укола и +6% к скорости перезарядки (арбалеты / ручницы).
    ///     4. «Второе дыхание»: +1 HP каждые 3 сек вне боя (если не получал урон 5 сек), но не выше 75% от макс. HP.
    /// - Бонус самому Знаменосцу (компенсация отсутствия щита, пока знамя в руке):
    ///     1. +6 к броне всех зон.
    ///     2. +5% к скорости марша и +6% к боевой скорости шага.
    ///     3. +5% к скорости удара/укола + регенерация «Второе дыхание» (до 75% HP вне боя).
    /// </summary>
    public class DWDN_BannerAuraBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        // Радиус ауры (12 метров -> квадрат расстояния = 144)
        public const float AuraRadiusMeters = 12.0f;
        public const float AuraRadiusSq = AuraRadiusMeters * AuraRadiusMeters;

        // Интервалы тиков
        private const float AuraCheckInterval = 0.5f;
        private const float RegenTickInterval = 3.0f;
        private const float OutOfCombatDelaySeconds = 5.0f;
        private const float MaxRegenHealthRatio = 0.75f; // Лечит только до 75% HP

        // Бонусы для союзников в строю (Tier 1)
        private const float AllyArmorBonus = 4.0f;
        private const float AllyMoveSpeedMult = 1.05f;
        private const float AllyCombatSpeedMult = 1.06f;
        private const float AllyAttackSpeedMult = 1.04f;
        private const float AllyReloadSpeedMult = 1.06f;

        // Бонусы для самого Знаменосца с поднятым знаменем (Tier 2 — компенсация отсутствия щита)
        private const float BearerArmorBonus = 6.0f;
        private const float BearerMoveSpeedMult = 1.05f;
        private const float BearerCombatSpeedMult = 1.06f;
        private const float BearerAttackSpeedMult = 1.05f;

        private class AgentAuraState
        {
            public bool BaseStatsCaptured;
            public float BaseArmorHead;
            public float BaseArmorTorso;
            public float BaseArmorLegs;
            public float BaseArmorArms;
            public float BaseCombatSpeed;

            public int CurrentTier; // 0 = нет ауры, 1 = союзник в ауре, 2 = сам знаменосец
            public EquipmentIndex LastPrimaryWielded = EquipmentIndex.None;
            public EquipmentIndex LastOffhandWielded = EquipmentIndex.None;
            public float LastDamagedMissionTime = -999f;
            public float LastUiNotificationTime = -999f;
        }

        private readonly Dictionary<int, AgentAuraState> _agentStates = new Dictionary<int, AgentAuraState>();
        private readonly List<Agent> _activeBannerBearers = new List<Agent>(16);

        private float _auraTimer = 0f;
        private float _regenTimer = 0f;
#if !SERVER
        private float _vfxTimer = 0f;
#endif

        public override void OnClearScene()
        {
            base.OnClearScene();
            _agentStates.Clear();
            _activeBannerBearers.Clear();
        }

        public override void OnRemoveBehavior()
        {
            _agentStates.Clear();
            _activeBannerBearers.Clear();
            base.OnRemoveBehavior();
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if (affectedAgent != null)
            {
                _agentStates.Remove(affectedAgent.Index);
            }
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            base.OnAgentDeleted(affectedAgent);
            if (affectedAgent != null)
            {
                _agentStates.Remove(affectedAgent.Index);
            }
        }

        public override void OnAgentHit(
            Agent affectedAgent,
            Agent affectorAgent,
            in MissionWeapon affectorWeapon,
            in Blow blow,
            in AttackCollisionData attackCollisionData)
        {
            base.OnAgentHit(affectedAgent, affectorAgent, in affectorWeapon, in blow, in attackCollisionData);

            if (affectedAgent != null && affectedAgent.IsHuman && blow.InflictedDamage > 0 && Mission != null)
            {
                if (!_agentStates.TryGetValue(affectedAgent.Index, out var state))
                {
                    state = new AgentAuraState();
                    _agentStates[affectedAgent.Index] = state;
                }
                state.LastDamagedMissionTime = Mission.CurrentTime;
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (Mission == null || Mission.Agents == null)
                return;

            _auraTimer += dt;
            _regenTimer += dt;
#if !SERVER
            _vfxTimer += dt;
#endif

            if (_auraTimer >= AuraCheckInterval)
            {
                _auraTimer = 0f;
                bool doRegenNow = false;
                if (_regenTimer >= RegenTickInterval)
                {
                    _regenTimer = 0f;
                    doRegenNow = true;
                }

                bool doVfxNow = false;
#if !SERVER
                if (_vfxTimer >= 2.5f)
                {
                    _vfxTimer = 0f;
                    doVfxNow = true;
                }
#endif

                UpdateAllAgentsAura(doRegenNow, doVfxNow);
            }
        }

        private void UpdateAllAgentsAura(bool doRegenNow, bool doVfxNow)
        {
            _activeBannerBearers.Clear();
            var agents = Mission.Agents;
            int agentCount = agents.Count;

            // 1. Находим всех живых знаменосцев, которые прямо сейчас держат знамя в руках
            for (int i = 0; i < agentCount; i++)
            {
                Agent a = agents[i];
                if (a == null || !a.IsActive() || !a.IsHuman || a.Team == null)
                    continue;

                if (IsWieldingBanner(a))
                {
                    _activeBannerBearers.Add(a);
#if !SERVER
                    if (doVfxNow && !GameNetwork.IsDedicatedServer)
                    {
                        SpawnBannerInspirationSparkle(a);
                    }
#endif
                }
            }

            float currentTime = Mission.CurrentTime;
            int bearerCount = _activeBannerBearers.Count;

            // 2. Обновляем статус каждого живого пехотинца/всадника на поле боя
            for (int i = 0; i < agentCount; i++)
            {
                Agent agent = agents[i];
                if (agent == null || !agent.IsActive() || !agent.IsHuman || agent.AgentDrivenProperties == null)
                    continue;

                if (!_agentStates.TryGetValue(agent.Index, out var state))
                {
                    state = new AgentAuraState();
                    _agentStates[agent.Index] = state;
                }

                if (!state.BaseStatsCaptured)
                {
                    CaptureBaseStats(agent, state);
                }

                // Определяем целевой уровень ауры (не стакается: 2 = сам знаменосец, 1 = союзник в 12м, 0 = вне ауры)
                int targetTier = 0;
                if (bearerCount > 0 && agent.Team != null)
                {
                    Vec3 pos = agent.Position;
                    for (int bIdx = 0; bIdx < bearerCount; bIdx++)
                    {
                        Agent bearer = _activeBannerBearers[bIdx];
                        if (bearer == agent)
                        {
                            targetTier = 2;
                            break;
                        }
                        if (bearer.Team == agent.Team && pos.DistanceSquared(bearer.Position) <= AuraRadiusSq)
                        {
                            targetTier = 1;
                        }
                    }
                }

                EquipmentIndex curPrim = agent.GetPrimaryWieldedItemIndex();
                EquipmentIndex curOff = agent.GetOffhandWieldedItemIndex();
                bool weaponChanged = curPrim != state.LastPrimaryWielded || curOff != state.LastOffhandWielded;
                bool tierChanged = targetTier != state.CurrentTier;

                if (tierChanged || (targetTier > 0 && weaponChanged))
                {
                    int previousTier = state.CurrentTier;
                    state.CurrentTier = targetTier;
                    state.LastPrimaryWielded = curPrim;
                    state.LastOffhandWielded = curOff;

                    ApplyAuraStatsToAgent(agent, state, targetTier);

#if !SERVER
                    if (!GameNetwork.IsDedicatedServer && agent.IsMainAgent && tierChanged)
                    {
                        NotifyLocalPlayerAuraChange(state, previousTier, targetTier, currentTime);
                    }
#endif
                }
                else
                {
                    state.LastPrimaryWielded = curPrim;
                    state.LastOffhandWielded = curOff;
                }

                // 3. Серверная мягкая регенерация «Второе дыхание» (+1 HP раз в 3 сек вне боя, до 75% от макс. HP)
                if (doRegenNow && targetTier > 0 && (GameNetwork.IsServerOrRecorder || !GameNetwork.IsMultiplayer))
                {
                    if (currentTime - state.LastDamagedMissionTime >= OutOfCombatDelaySeconds)
                    {
                        float maxRegenCap = TaleWorlds.Library.MathF.Floor(agent.HealthLimit * MaxRegenHealthRatio);
                        if (agent.Health > 0f && agent.Health < maxRegenCap)
                        {
                            float newHp = TaleWorlds.Library.MathF.Min(maxRegenCap, agent.Health + 1.0f);
                            agent.Health = newHp; // Автоматически вызывает SyncHealthToClients() на сервере
                        }
                    }
                }
            }
        }

        private static bool IsWieldingBanner(Agent agent)
        {
            if (agent == null || agent.Equipment == null) return false;

            // Проверяем левую руку (Offhand — стандартный слот для знамени с HeldInOffHand="true")
            EquipmentIndex offIdx = agent.GetOffhandWieldedItemIndex();
            if (offIdx != EquipmentIndex.None && !agent.Equipment[offIdx].IsEmpty)
            {
                ItemObject offItem = agent.Equipment[offIdx].Item;
                if (IsBannerItem(offItem)) return true;
            }

            // Проверяем правую руку (Primary — если знамя взято в основную руку)
            EquipmentIndex primIdx = agent.GetPrimaryWieldedItemIndex();
            if (primIdx != EquipmentIndex.None && !agent.Equipment[primIdx].IsEmpty)
            {
                ItemObject primItem = agent.Equipment[primIdx].Item;
                if (IsBannerItem(primItem)) return true;
            }

            return false;
        }

        private static bool IsBannerItem(ItemObject item)
        {
            if (item == null) return false;
            if (item.ItemType == ItemObject.ItemTypeEnum.Banner) return true;
            string id = item.StringId;
            return !string.IsNullOrEmpty(id) && id.IndexOf("banner", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CaptureBaseStats(Agent agent, AgentAuraState state)
        {
            var dp = agent.AgentDrivenProperties;
            if (dp == null) return;

            state.BaseArmorHead = dp.ArmorHead;
            state.BaseArmorTorso = dp.ArmorTorso;
            state.BaseArmorLegs = dp.ArmorLegs;
            state.BaseArmorArms = dp.ArmorArms;
            state.BaseCombatSpeed = dp.CombatMaxSpeedMultiplier > 0.1f ? dp.CombatMaxSpeedMultiplier : 0.85f;
            state.BaseStatsCaptured = true;
        }

        private static void ApplyAuraStatsToAgent(Agent agent, AgentAuraState state, int tier)
        {
            try
            {
                // 1. Сначала пересчитываем чистые ванильные динамические статы (бег, скорость удара, перезарядка) для текущего оружия
                agent.UpdateAgentProperties();

                var dp = agent.AgentDrivenProperties;
                if (dp == null) return;

                if (tier == 0)
                {
                    // Возвращаем базовую броню и боевую скорость шага
                    dp.ArmorHead = state.BaseArmorHead;
                    dp.ArmorTorso = state.BaseArmorTorso;
                    dp.ArmorLegs = state.BaseArmorLegs;
                    dp.ArmorArms = state.BaseArmorArms;
                    dp.CombatMaxSpeedMultiplier = state.BaseCombatSpeed;
                }
                else if (tier == 1)
                {
                    // Союзник в радиусе 12м от знамени
                    dp.ArmorHead = state.BaseArmorHead + AllyArmorBonus;
                    dp.ArmorTorso = state.BaseArmorTorso + AllyArmorBonus;
                    dp.ArmorLegs = state.BaseArmorLegs + AllyArmorBonus;
                    dp.ArmorArms = state.BaseArmorArms + AllyArmorBonus;

                    dp.CombatMaxSpeedMultiplier = state.BaseCombatSpeed * AllyCombatSpeedMult;
                    dp.MaxSpeedMultiplier *= AllyMoveSpeedMult;
                    dp.SwingSpeedMultiplier *= AllyAttackSpeedMult;
                    dp.ThrustOrRangedReadySpeedMultiplier *= AllyAttackSpeedMult;
                    dp.ReloadSpeed *= AllyReloadSpeedMult;
                }
                else if (tier == 2)
                {
                    // Сам Знаменосец с поднятым знаменем (повышенная защита +6 к броне из-за отсутствия щита)
                    dp.ArmorHead = state.BaseArmorHead + BearerArmorBonus;
                    dp.ArmorTorso = state.BaseArmorTorso + BearerArmorBonus;
                    dp.ArmorLegs = state.BaseArmorLegs + BearerArmorBonus;
                    dp.ArmorArms = state.BaseArmorArms + BearerArmorBonus;

                    dp.CombatMaxSpeedMultiplier = state.BaseCombatSpeed * BearerCombatSpeedMult;
                    dp.MaxSpeedMultiplier *= BearerMoveSpeedMult;
                    dp.SwingSpeedMultiplier *= BearerAttackSpeedMult;
                    dp.ThrustOrRangedReadySpeedMultiplier *= BearerAttackSpeedMult;
                    dp.ReloadSpeed *= AllyReloadSpeedMult;
                }

                // 2. Передаем обновленный массив 98 свойств напрямую в нативный движок C++
                agent.UpdateCustomDrivenProperties();
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN BannerAura ERROR] ApplyAuraStatsToAgent: {ex.Message}");
            }
        }

#if !SERVER
        private static void SpawnBannerInspirationSparkle(Agent bearer)
        {
            try
            {
                Vec3 tipPos = bearer.Position + new Vec3(0f, 0f, 2.25f);
                MatrixFrame frame = MatrixFrame.Identity;
                frame.origin = tipPos;
                Mission.Current?.AddParticleSystemBurstByName("psys_game_sparkle_a", frame, false);
            }
            catch
            {
            }
        }

        private static void NotifyLocalPlayerAuraChange(AgentAuraState state, int oldTier, int newTier, float currentTime)
        {
            // Анти-спам: не чаще одного уведомления в 4 секунды при беге по границе круга
            if (currentTime - state.LastUiNotificationTime < 4.0f && newTier == 0)
                return;

            state.LastUiNotificationTime = currentTime;

            if (newTier == 2)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "⚜️ Знамя поднято! Вы воодушевляете строй (12м): +6 Броня вам, +4 Броня и +5% Скорость союзникам",
                    Color.FromUint(0xFFFFD700) // Золотой
                ));
            }
            else if (newTier == 1 && oldTier == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "⚜️ Воодушевление знамени: +4 Броня, +5% Скорость, +4% Атака, Второе дыхание (до 75% HP)",
                    Color.FromUint(0xFF4ADE80) // Изумрудно-зелёный
                ));
            }
            else if (newTier == 0 && oldTier > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "⚜️ Действие ауры знамени прекратилось.",
                    Color.FromUint(0xFF94A3B8) // Приглушённо-серый
                ));
            }
        }
#endif
    }
}
