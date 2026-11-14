using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DWDN_Script
{
    /// <summary>
    /// Механика ручных гранат DWDN (AoE детонация с баллистикой и физикой взрывной волны).
    /// Отслеживает брошенные снаряды dwdn_grenade, при столкновении (OnMissileHit)
    /// производит детонацию: визуальные эффекты и звук для клиентов,
    /// экранирование укрытиями (RayCast) и урон со сбиванием с ног (KnockDown) на сервере.
    /// </summary>
    public class DWDN_GrenadeBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private class GrenadeData
        {
            public Agent Thrower;
            public float SpawnTime;
        }

        private readonly Dictionary<int, GrenadeData> _activeGrenades = new Dictionary<int, GrenadeData>();

        public const float ExplosionRadius = 6.0f;
        public const float MaxDamage = 135.0f;
        public const float MinDamage = 25.0f;
        public const float KnockdownRadius = 2.5f;

        private int _soundExplosion = -1;
        private bool _soundInitialized = false;

        public override void OnAgentShootMissile(
            Agent shooterAgent,
            EquipmentIndex weaponIndex,
            Vec3 position,
            Vec3 velocity,
            Mat3 orientation,
            bool hasRigidBody,
            int forcedMissileIndex)
        {
            base.OnAgentShootMissile(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);

            if (shooterAgent == null || Mission == null) return;

            try
            {
                bool isGrenade = false;

                // 1. Проверка экипированного слота бросившего
                if (weaponIndex >= EquipmentIndex.WeaponItemBeginSlot && weaponIndex < EquipmentIndex.NumAllWeaponSlots)
                {
                    MissionWeapon weapon = shooterAgent.Equipment[weaponIndex];
                    if (weapon.Item != null && weapon.Item.StringId == "dwdn_grenade")
                    {
                        isGrenade = true;
                    }
                }

                // 2. Проверка зарегистрированного снаряда в MissilesList
                int missileIndex = forcedMissileIndex;
                if (Mission.MissilesList != null && Mission.MissilesList.Count > 0)
                {
                    var lastMissile = Mission.MissilesList[Mission.MissilesList.Count - 1];
                    if (lastMissile.Weapon.Item != null && lastMissile.Weapon.Item.StringId == "dwdn_grenade")
                    {
                        isGrenade = true;
                        if (missileIndex < 0)
                        {
                            missileIndex = lastMissile.Index;
                        }
                    }
                }

                if (isGrenade && missileIndex >= 0)
                {
                    _activeGrenades[missileIndex] = new GrenadeData
                    {
                        Thrower = shooterAgent,
                        SpawnTime = Mission.CurrentTime
                    };
                }
            }
            catch
            {
            }
        }

        public override void OnMissileHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            base.OnMissileHit(attacker, victim, isCanceled, collisionData);

            if (Mission == null) return;

            int missileIndex = collisionData.AffectorWeaponSlotOrMissileIndex;
            Agent thrower = null;
            bool isGrenade = false;

            if (_activeGrenades.TryGetValue(missileIndex, out var gData))
            {
                isGrenade = true;
                thrower = gData.Thrower;
                _activeGrenades.Remove(missileIndex);
            }
            else if (collisionData.IsMissile && IsMissileGrenade(missileIndex, out thrower))
            {
                isGrenade = true;
                _activeGrenades.Remove(missileIndex);
            }

            if (isGrenade)
            {
                // Принудительно очищаем меш снаряда на клиенте, если движок не успел удалить его сам
                if (!GameNetwork.IsDedicatedServer)
                {
                    try
                    {
                        Mission.RemoveMissileAsClient(missileIndex);
                    }
                    catch
                    {
                    }
                }

                // Определение эпицентра взрыва с поднятием над плоскостью столкновения
                Vec3 epicenter = collisionData.CollisionGlobalPosition;
                if (epicenter.LengthSquared < 0.01f)
                {
                    if (victim != null) epicenter = victim.GetChestGlobalPosition();
                    else if (attacker != null) epicenter = attacker.Position;
                }

                if (collisionData.CollisionGlobalNormal.LengthSquared > 0.01f)
                {
                    epicenter += collisionData.CollisionGlobalNormal * 0.2f;
                }
                else
                {
                    epicenter.z += 0.2f;
                }

                Detonate(thrower ?? attacker, epicenter);
            }
        }

        private bool IsMissileGrenade(int missileIndex, out Agent thrower)
        {
            thrower = null;
            if (Mission?.MissilesList == null) return false;
            for (int i = 0; i < Mission.MissilesList.Count; i++)
            {
                var m = Mission.MissilesList[i];
                if (m.Index == missileIndex && m.Weapon.Item?.StringId == "dwdn_grenade")
                {
                    thrower = m.ShooterAgent;
                    return true;
                }
            }
            return false;
        }

        public override void OnMissileRemoved(int missileIndex)
        {
            base.OnMissileRemoved(missileIndex);
            // ВАЖНО: Не удаляем гранату из _activeGrenades здесь!
            // В Bannerlord HandleMissileCollisionReaction вызывает RemoveMissile (и OnMissileRemoved)
            // ПЕРЕД тем, как передать управление в OnMissileHit.
            // Удаление гранаты производится при обработке в OnMissileHit или по таймауту в OnMissionTick.
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            // Очистка записей не разорвавшихся гранат (например, улетевших за границы сцены) старше 15 секунд
            if (_activeGrenades.Count > 0 && Mission != null)
            {
                float curTime = Mission.CurrentTime;
                List<int> expired = null;
                foreach (var kvp in _activeGrenades)
                {
                    if (curTime - kvp.Value.SpawnTime > 15.0f)
                    {
                        if (expired == null) expired = new List<int>();
                        expired.Add(kvp.Key);
                    }
                }
                if (expired != null)
                {
                    for (int i = 0; i < expired.Count; i++)
                    {
                        _activeGrenades.Remove(expired[i]);
                    }
                }
            }
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            _activeGrenades.Clear();
        }

        public override void OnRemoveBehavior()
        {
            _activeGrenades.Clear();
            base.OnRemoveBehavior();
        }

        private void Detonate(Agent thrower, Vec3 epicenter)
        {
            // 1. Клиентские эффекты взрыва (Client / Singleplayer)
            if (!GameNetwork.IsDedicatedServer)
            {
                SpawnExplosionVFX(epicenter);

                try
                {
                    InformationManager.DisplayMessage(new InformationMessage("[DWDN] Взрыв гранаты!", Colors.Red));
                }
                catch
                {
                }
            }

            // 2. Серверная регистрация урона по площади (Server / Singleplayer)
            if (GameNetwork.IsClient)
                return; // Клиенты в мультиплеере не регистрируют урон

            ApplyBlastDamage(thrower, epicenter);
        }

        private void SpawnExplosionVFX(Vec3 epicenter)
        {
            try
            {
                MatrixFrame frame = MatrixFrame.Identity;
                frame.origin = epicenter;

                // Вспышка и искры пороха
                Mission.AddParticleSystemBurstByName("psys_game_sparkle_a", frame, false);

                // Эффект детонации зажигательной смеси / осколков
                Mission.AddParticleSystemBurstByName("psys_burning_projectile_default_coll", frame, false);

                // Густое облако дыма и пыли от взрыва
                Mission.AddParticleSystemBurstByName("psys_game_stone_dust_a", frame, false);
                Mission.AddParticleSystemBurstByName("psys_siege_door_falling_dust", frame, false);

                // Звук разрыва
                if (!_soundInitialized)
                {
                    _soundExplosion = SoundEvent.GetEventIdFromString("event:/mission/combat/impact/ballista_missile/stone");
                    _soundInitialized = true;
                }
                if (_soundExplosion >= 0)
                {
                    Mission.MakeSound(_soundExplosion, epicenter, true, true, -1, -1);
                }
            }
            catch
            {
            }
        }

        private void ApplyBlastDamage(Agent thrower, Vec3 epicenter)
        {
            if (Mission?.Agents == null) return;

            List<Agent> agentsSnapshot;
            try
            {
                agentsSnapshot = new List<Agent>(Mission.Agents);
            }
            catch
            {
                return;
            }

            foreach (Agent agent in agentsSnapshot)
            {
                try
                {
                    if (agent == null || !agent.IsActive()) continue;

                    Vec3 targetPos = agent.GetChestGlobalPosition();
                    Vec3 diff = targetPos - epicenter;
                    float distance = diff.Length;

                    if (distance > ExplosionRadius) continue;

                    // Проверка экранирования препятствиями (RayCast)
                    try
                    {
                        if (Mission.Scene.RayCastForClosestEntityOrTerrain(epicenter, targetPos, out float collisionDist))
                        {
                            if (collisionDist < distance - 0.4f)
                            {
                                // Препятствие перекрыло прямую видимость взрыва
                                continue;
                            }
                        }
                    }
                    catch
                    {
                    }

                    // Линейный спад урона (Lerp)
                    float t = 1.0f - (distance / ExplosionRadius);
                    if (t < 0f) t = 0f;
                    if (t > 1f) t = 1f;

                    float damage = MinDamage + (MaxDamage - MinDamage) * t;
                    Vec3 blastDir = distance > 0.05f ? (diff / distance) : Vec3.Up;

                    Blow blow = new Blow(thrower?.Index ?? -1)
                    {
                        DamageType = DamageTypes.Blunt,
                        BlowFlag = (distance <= KnockdownRadius && !agent.IsMount) ? BlowFlags.KnockDown : BlowFlags.None,
                        GlobalPosition = targetPos,
                        Direction = blastDir,
                        SwingDirection = blastDir,
                        BaseMagnitude = damage,
                        InflictedDamage = (int)damage,
                        VictimBodyPart = BoneBodyPartType.Chest,
                        DamageCalculated = true
                    };

                    AttackCollisionData collisionData = default;
                    agent.RegisterBlow(blow, in collisionData);
                }
                catch
                {
                }
            }
        }
    }
}
