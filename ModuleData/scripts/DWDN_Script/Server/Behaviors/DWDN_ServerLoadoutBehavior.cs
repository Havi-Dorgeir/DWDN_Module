using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;
using NetworkMessages.FromServer;
using TaleWorlds.ObjectSystem;
using DWDN_Script.Common.Duchies;
using DWDN_Script.Common.Network;

namespace DWDN_Script.Server.Behaviors
{
    /// <summary>
    /// Серверное поведение экипировки DWDN:
    /// 1. Принимает кастомный выбор экипировки от клиента и сохраняет его.
    /// 2. Применяет выбранный лодаут на агента при спавне и смене снаряжения.
    /// 3. Корректно синхронизирует боеприпасы и оружие без рассинхронизации фаз перезарядки.
    /// </summary>
    public class DWDN_ServerLoadoutBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public static DWDN_ServerLoadoutBehavior Instance { get; private set; }

        // Хранилище выборов: ключ - Index сетевого игрока (NetworkCommunicator.Index)
        private static readonly Dictionary<int, DWDN_LoadoutSelection> _peerSelections = new Dictionary<int, DWDN_LoadoutSelection>();
        private static DWDN_LoadoutSelection _localPlayerSelection = null;
        private static DWDN_LoadoutSelection _lastReceivedSelection = null;

        // Список агентов, которым уже применен кастомный лодаут
        private static readonly HashSet<int> _customizedAgentIndices = new HashSet<int>();

        // Агенты, у которых сняли коня и ждут, пока он уйдёт, — время снятия.
        // Переодевать всадника в том же такте, что и снимать коня, нельзя (см. TryApplyLoadoutToAgent).
        private static readonly Dictionary<int, float> _dismountPending = new Dictionary<int, float>();
        private const float DismountWaitSeconds = 2f;
        private static bool _handlerRegistered = false;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Instance = this;
            EnsureHandlerRegistered();
        }

        public override void EarlyStart()
        {
            base.EarlyStart();
            EnsureHandlerRegistered();
        }

        public override void AfterStart()
        {
            base.AfterStart();
            EnsureHandlerRegistered();
        }

        private void EnsureHandlerRegistered()
        {
            if (_handlerRegistered) return;

            if (GameNetwork.IsServer)
            {
                DWDN_NetworkHelper.RegisterClientMessageHandler<RequestCustomLoadoutMessage>(HandleClientCustomLoadout);
                _handlerRegistered = true;
                Debug.Print("[DWDN Server] Обработчик RequestCustomLoadoutMessage зарегистрирован.");
            }
        }

        public override void OnRemoveBehavior()
        {
            if (GameNetwork.IsServer && _handlerRegistered)
            {
                DWDN_NetworkHelper.UnregisterClientMessageHandler<RequestCustomLoadoutMessage>(HandleClientCustomLoadout);
                _handlerRegistered = false;
            }
            _customizedAgentIndices.Clear();
            if (Instance == this)
            {
                Instance = null;
            }
            base.OnRemoveBehavior();
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            _customizedAgentIndices.Clear();
            _dismountPending.Clear();
        }

        private bool HandleClientCustomLoadout(NetworkCommunicator peer, RequestCustomLoadoutMessage msg)
        {
            if (peer == null || msg == null)
                return false;

            try
            {
                DWDN_LoadoutSelection selection = msg.ToSelection();
                int peerKey = peer.Index;
                _peerSelections[peerKey] = selection;
                if (peer.VirtualPlayer != null)
                {
                    _peerSelections[peer.VirtualPlayer.Index] = selection;
                }
                _lastReceivedSelection = selection;

                Debug.Print($"[DWDN Server] Получен кастомный лодаут от игрока {peer.UserName} (Index={peerKey}): Duchy={selection.DuchyId}, Class={selection.ClassId}, W0={selection.Weapon0Id}, W1={selection.Weapon1Id}, W2={selection.Weapon2Id}, W3={selection.Weapon3Id}, Head={selection.HeadId}, Body={selection.BodyId}");

                // Синхронизируем класс в нативном MissionPeer для спавна
                MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
                if (missionPeer != null)
                {
                    var duchy = DWDN_DuchyManager.GetDuchy(selection.DuchyId);
                    var cls = duchy?.GetClass(selection.ClassId);
                    if (cls != null)
                    {
                        // Безопасный нативный индекс для спавна и синхронизации с культурой команды игрока
                        BasicCultureObject peerCulture = missionPeer.Culture;
                        if (peerCulture == null && missionPeer.Team != null)
                        {
                            string cultStr = missionPeer.Team.Side == BattleSideEnum.Attacker
                                ? MultiplayerOptions.OptionType.CultureTeam1.GetStrValue()
                                : MultiplayerOptions.OptionType.CultureTeam2.GetStrValue();
                            peerCulture = MBObjectManager.Instance?.GetObject<BasicCultureObject>(cultStr);
                        }
                        if (peerCulture != null)
                        {
                            var peerClasses = MultiplayerClassDivisions.GetMPHeroClasses(peerCulture);
                            if (peerClasses != null)
                            {
                                var classList = System.Linq.Enumerable.ToList(peerClasses);
                                if (classList.Count > 0)
                                {
                                    int safeNativeIdx = 0;
                                    if (!string.IsNullOrEmpty(selection.HorseId))
                                    {
                                        int cavIdx = classList.FindIndex(c => c != null && c.HeroCharacter != null && c.HeroCharacter.IsMounted);
                                        if (cavIdx >= 0) safeNativeIdx = cavIdx;
                                    }
                                    safeNativeIdx = Math.Min(Math.Max(0, safeNativeIdx), classList.Count - 1);
                                    missionPeer.SelectedTroopIndex = safeNativeIdx;
                                    missionPeer.NextSelectedTroopIndex = safeNativeIdx;
                                    try
                                    {
                                        GameNetwork.BeginBroadcastModuleEvent();
                                        GameNetwork.WriteMessage(new UpdateSelectedTroopIndex(peer, safeNativeIdx));
                                        GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.ExcludeOtherTeamPlayers, peer);
                                    }
                                    catch {}
                                }
                            }
                        }
                    }

                    // Если агент игрока уже жив на поле боя (например, в warmup или при смене снаряги), сразу обновляем его экипировку
                    Agent agent = missionPeer.ControlledAgent;
                    if (agent != null && agent.IsHuman && agent.IsActive())
                    {
                        _customizedAgentIndices.Remove(agent.Index);
                        TryApplyLoadoutToAgent(agent, missionPeer, peerKey);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Server ERROR] Ошибка в обработке лодаута: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Устанавливает локальный лодаут (для синглплеера или локального тестирования)
        /// </summary>
        public static void SetLocalPlayerLoadout(DWDN_LoadoutSelection selection)
        {
            _localPlayerSelection = selection?.Clone();
            if (Agent.Main != null && Agent.Main.IsActive())
            {
                _customizedAgentIndices.Remove(Agent.Main.Index);
                Instance?.TryApplyLoadoutToAgent(Agent.Main);
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (!GameNetwork.IsServer)
                return;

            EnsureHandlerRegistered();

            var peers = GameNetwork.NetworkPeers;
            if (peers == null) return;

            for (int i = 0; i < peers.Count; i++)
            {
                var netPeer = peers[i];
                if (netPeer == null || !netPeer.IsSynchronized) continue;

                var missionPeer = netPeer.GetComponent<MissionPeer>();
                if (missionPeer == null) continue;

                Agent agent = missionPeer.ControlledAgent;
                if (agent != null && agent.IsActive() && agent.IsHuman)
                {
                    if (!_customizedAgentIndices.Contains(agent.Index))
                    {
                        TryApplyLoadoutToAgent(agent, missionPeer, netPeer.Index);
                    }
                }
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
        }

        public override void OnAgentControllerSetToPlayer(Agent agent)
        {
            base.OnAgentControllerSetToPlayer(agent);
            if (!GameNetwork.IsServer) return;
            TryApplyLoadoutToAgent(agent);
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if (affectedAgent != null)
            {
                _customizedAgentIndices.Remove(affectedAgent.Index);
                _dismountPending.Remove(affectedAgent.Index);
            }
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            base.OnAgentDeleted(affectedAgent);
            if (affectedAgent != null)
            {
                _customizedAgentIndices.Remove(affectedAgent.Index);
                _dismountPending.Remove(affectedAgent.Index);
            }
        }

        /// <summary>
        /// Возвращает целевой лодаут для агента на основе выбора игрока или класса.
        /// </summary>
        public static DWDN_LoadoutSelection GetSelectionForAgent(Agent agent, MissionPeer peer = null, int peerIndex = -1)
        {
            if (agent == null) return null;

            try
            {
                if (peer == null)
                {
                    peer = agent.MissionPeer;
                    if (peer == null && GameNetwork.NetworkPeers != null)
                    {
                        for (int i = 0; i < GameNetwork.NetworkPeers.Count; i++)
                        {
                            var np = GameNetwork.NetworkPeers[i];
                            if (np == null) continue;
                            if (np.ControlledAgent == agent)
                            {
                                peer = np.GetComponent<MissionPeer>();
                                peerIndex = np.Index;
                                break;
                            }
                            var mp = np.GetComponent<MissionPeer>();
                            if (mp != null && mp.ControlledAgent == agent)
                            {
                                peer = mp;
                                peerIndex = np.Index;
                                break;
                            }
                        }
                    }
                }

                if (peerIndex == -1 && peer != null)
                {
                    peerIndex = peer.GetNetworkPeer()?.Index ?? peer.Peer?.Index ?? -1;
                }

                DWDN_LoadoutSelection targetSelection = null;

                // 1. Попытка взять из UI по индексу пира
                if (peerIndex != -1 && _peerSelections.TryGetValue(peerIndex, out var s))
                {
                    targetSelection = s;
                }

                // 1b. Попытка взять через peer.Peer?.Index
                if (targetSelection == null && peer?.Peer != null && _peerSelections.TryGetValue(peer.Peer.Index, out var s2))
                {
                    targetSelection = s2;
                }

                // 1c. Попытка взять через peer.GetNetworkPeer()?.Index
                if (targetSelection == null && peer != null)
                {
                    var netPeer = peer.GetNetworkPeer();
                    if (netPeer != null && _peerSelections.TryGetValue(netPeer.Index, out var s3))
                    {
                        targetSelection = s3;
                    }
                }

                // 1d. Если только один игрок на сервере, берем последний полученный лодаут
                if (targetSelection == null && _lastReceivedSelection != null)
                {
                    targetSelection = _lastReceivedSelection;
                }

                // 2. Если выбор через UI еще не сделан, берем дефолтный лодаут для его класса
                if (targetSelection == null && peer != null)
                {
                    var heroClass = MultiplayerClassDivisions.GetMPHeroClassForPeer(peer);
                    if (heroClass != null)
                    {
                        foreach (var duchy in DWDN_DuchyManager.Duchies)
                        {
                            foreach (var cls in duchy.Classes)
                            {
                                if (string.Equals(cls.Id, heroClass.StringId, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(cls.TroopId, heroClass.HeroCharacter?.StringId, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(cls.TroopId, heroClass.TroopCharacter?.StringId, StringComparison.OrdinalIgnoreCase))
                                {
                                    targetSelection = cls.DefaultLoadout ?? cls.CreateDefaultLoadout(duchy.Id);
                                    break;
                                }
                            }
                            if (targetSelection != null) break;
                        }
                    }
                }

                // 3. Локальный игрок
                if (targetSelection == null && (agent.IsPlayerControlled || agent.IsMainAgent))
                {
                    targetSelection = _localPlayerSelection;
                }

                // 4. ЖЕЛЕЗОБЕТОННЫЙ FALLBACK: если лодаут все еще null, берем дефолтный класс Баннрайха
                if (targetSelection == null)
                {
                    var defaultDuchy = DWDN_DuchyManager.GetDuchy("banreich") ?? (DWDN_DuchyManager.Duchies.Count > 0 ? DWDN_DuchyManager.Duchies[0] : null);
                    if (defaultDuchy != null && defaultDuchy.Classes.Count > 0)
                    {
                        var defaultClass = defaultDuchy.Classes[0];
                        targetSelection = defaultClass.DefaultLoadout ?? defaultClass.CreateDefaultLoadout(defaultDuchy.Id);
                    }
                }

                return targetSelection;
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Server ERROR] GetSelectionForAgent: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Применяет кастомный выбор экипировки к агенту.
        /// </summary>
        public void TryApplyLoadoutToAgent(Agent agent, MissionPeer peer = null, int peerIndex = -1)
        {
            if (agent == null || !agent.IsHuman || !agent.IsActive()) return;

            if (_customizedAgentIndices.Contains(agent.Index))
                return;

            try
            {
                DWDN_LoadoutSelection targetSelection = GetSelectionForAgent(agent, peer, peerIndex);

                // 5. Применяем экипировку
                if (targetSelection != null)
                {
                    // Если игрок выбрал пеший бой, но у агента по какой-то причине есть лошадь (например, спавн в warmup) - спешиваем и удаляем её.
                    // ВАЖНО (14.11.2026): снять коня и в том же такте позвать UpdateSpawnEquipmentAndRefreshVisuals
                    // нельзя — на BL v1.5.7 это роняет ВЕСЬ выделенный сервер (segfault в Agent::preload_tick
                    // из PreloadForRendering; дважды за день на mp_ALberic_Devald_hero). Поэтому коня снимаем
                    // и выходим, а снаряжение надеваем в следующих тактах (OnMissionTick зовёт нас снова),
                    // когда агент уже пеший.
                    if (string.IsNullOrEmpty(targetSelection.HorseId) && agent.MountAgent != null)
                    {
                        float now = Mission.Current?.CurrentTime ?? 0f;
                        if (!_dismountPending.TryGetValue(agent.Index, out float since))
                        {
                            _dismountPending[agent.Index] = now;
                            try
                            {
                                Agent mount = agent.MountAgent;
                                agent.TeleportToPosition(mount.Position);
                                mount.FadeOut(hideInstantly: true, hideMount: false);
                            }
                            catch (Exception ex)
                            {
                                Debug.Print($"[DWDN Server ERROR] Despawn mount: {ex}");
                            }
                            return;
                        }
                        if (now - since < DismountWaitSeconds)
                            return;

                        // Конь так и не ушёл — не переодеваем вовсе: штатное снаряжение лучше упавшего сервера.
                        _dismountPending.Remove(agent.Index);
                        _customizedAgentIndices.Add(agent.Index);
                        Debug.Print($"[DWDN Server] Mount of {agent.Name} (Index: {agent.Index}) did not go away; custom loadout skipped.");
                        return;
                    }
                    _dismountPending.Remove(agent.Index);

                    Equipment baseEquipment = agent.SpawnEquipment ?? new Equipment();
                    Equipment customizedEquipment = ApplySelectionToEquipment(targetSelection, baseEquipment);

                    // Обновляем SpawnEquipment и рассылаем SynchronizeAgentSpawnEquipment
                    agent.UpdateSpawnEquipmentAndRefreshVisuals(customizedEquipment);

                    // Заряжаем оружие дальнего боя (арбалеты и ручницы) штатным методом движка
                    try
                    {
                        agent.Equipment?.CheckLoadedAmmos();
                    }
                    catch (Exception ex)
                    {
                        Debug.Print($"[DWDN Server ERROR] CheckLoadedAmmos: {ex}");
                    }

                    agent.CheckEquipmentForCapeClothSimulationStateChange();
                    agent.UpdateWeapons();
                    agent.WieldInitialWeapons();

                    // Если первичное оружие не взято в руки, гарантированно берем оружие из слота 0 (оружие ближнего боя)
                    if (agent.GetPrimaryWieldedItemIndex() == EquipmentIndex.None && !agent.Equipment[EquipmentIndex.WeaponItemBeginSlot].IsEmpty)
                    {
                        agent.TryToWieldWeaponInSlot(EquipmentIndex.WeaponItemBeginSlot, Agent.WeaponWieldActionType.Instant, false);
                    }

                    // Гарантированно берем оффхенд предмет (знамя/щит в слоте 1) в левую руку
                    if (agent.GetOffhandWieldedItemIndex() == EquipmentIndex.None && !agent.Equipment[EquipmentIndex.Weapon1].IsEmpty)
                    {
                        var offhand = agent.Equipment[EquipmentIndex.Weapon1];
                        if (offhand.Item?.ItemType == ItemObject.ItemTypeEnum.Banner || offhand.Item?.ItemType == ItemObject.ItemTypeEnum.Shield || offhand.Item?.ItemFlags.HasAnyFlag(ItemFlags.HeldInOffHand) == true)
                        {
                            agent.TryToWieldWeaponInSlot(EquipmentIndex.Weapon1, Agent.WeaponWieldActionType.Instant, true);
                        }
                    }

                    // Синхронизируем оружие в руках
                    if (GameNetwork.IsServerOrRecorder)
                    {
                        EquipmentIndex wieldedPrimary = agent.GetPrimaryWieldedItemIndex();
                        if (wieldedPrimary != EquipmentIndex.None && !agent.Equipment[wieldedPrimary].IsEmpty)
                        {
                            GameNetwork.BeginBroadcastModuleEvent();
                            GameNetwork.WriteMessage(new SetWieldedItemIndex(agent.Index, false, true, true, wieldedPrimary, agent.Equipment[wieldedPrimary].CurrentUsageIndex));
                            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.AddToMissionRecord, null);
                        }

                        EquipmentIndex wieldedOffhand = agent.GetOffhandWieldedItemIndex();
                        if (wieldedOffhand != EquipmentIndex.None && !agent.Equipment[wieldedOffhand].IsEmpty)
                        {
                            GameNetwork.BeginBroadcastModuleEvent();
                            GameNetwork.WriteMessage(new SetWieldedItemIndex(agent.Index, true, true, true, wieldedOffhand, 0));
                            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.AddToMissionRecord, null);
                        }
                    }

                    _customizedAgentIndices.Add(agent.Index);
                    Debug.Print($"[DWDN Server] Loadout applied to {agent.Name} (Index: {agent.Index}) for class {targetSelection.ClassId}");

                    // Проверяем заявку на полевую артиллерию после применения кастомного лодаута
                    // ВРЕМЕННО ОТКЛЮЧЕНО (22.11.2026) вместе с классом артиллериста: после появления онагра
                    // с тягловой лошадью сервер завис в бесконечном цикле главного потока. См. issue #12.
                    // try
                    // {
                    //     DWDN_FieldArtilleryBehavior.CheckAndSpawnArtilleryForAgent(agent);
                    // }
                    // catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Server EXCEPTION] TryApplyLoadoutToAgent: {ex}");
            }
        }

        private Equipment ApplySelectionToEquipment(DWDN_LoadoutSelection selection, Equipment baseEquip)
        {
            Equipment equip = new Equipment(baseEquip);

            void SetSlot(EquipmentIndex index, string itemId)
            {
                if (string.IsNullOrEmpty(itemId))
                {
                    equip[index] = EquipmentElement.Invalid;
                    return;
                }

                ItemObject item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
                if (item != null)
                {
                    // Защита от скрапкода: в оружейных слотах предмет должен иметь PrimaryWeapon либо быть предметом для левой руки / щитом / знаменем
                    if (index >= EquipmentIndex.WeaponItemBeginSlot && index <= EquipmentIndex.ExtraWeaponSlot)
                    {
                        if (item.PrimaryWeapon == null && !item.ItemFlags.HasAnyFlag(ItemFlags.HeldInOffHand) && item.ItemType != ItemObject.ItemTypeEnum.Banner && item.ItemType != ItemObject.ItemTypeEnum.Shield)
                        {
                            Debug.Print($"[DWDN Server ERROR] Оружие '{itemId}' в слоте {index} имеет PrimaryWeapon == null! Слот очищен для предотвращения краша.");
                            equip[index] = EquipmentElement.Invalid;
                            return;
                        }
                    }
                    equip[index] = new EquipmentElement(item);
                }
                else
                {
                    Debug.Print($"[DWDN Server WARNING] Предмет с id='{itemId}' не найден в MBObjectManager для слота {index}!");
                    equip[index] = EquipmentElement.Invalid;
                }
            }

            // Броня
            SetSlot(EquipmentIndex.Head, selection.HeadId);
            SetSlot(EquipmentIndex.Cape, selection.CapeId);
            SetSlot(EquipmentIndex.Body, selection.BodyId);
            SetSlot(EquipmentIndex.Gloves, selection.GlovesId);
            SetSlot(EquipmentIndex.Leg, selection.LegsId);

            // Оружие
            SetSlot(EquipmentIndex.WeaponItemBeginSlot + 0, selection.Weapon0Id);
            SetSlot(EquipmentIndex.WeaponItemBeginSlot + 1, selection.Weapon1Id);
            SetSlot(EquipmentIndex.WeaponItemBeginSlot + 2, selection.Weapon2Id);
            SetSlot(EquipmentIndex.WeaponItemBeginSlot + 3, selection.Weapon3Id);
            equip[EquipmentIndex.ExtraWeaponSlot] = EquipmentElement.Invalid;

            // Человек-всадник никогда не должен нести слоты Horse / HorseHarness в своём SpawnEquipment!
            // Иначе при UpdateSpawnEquipmentAndRefreshVisuals движок Native вызывает
            // MountVisualCreator.AddMountMeshToAgentVisual на человеке, привязывая меши сбруи и коня
            // к скелету человека, которые при спешивании опадают на землю в точке (0,0,0).
            equip[EquipmentIndex.Horse] = EquipmentElement.Invalid;
            equip[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;

            return equip;
        }
    }
}
