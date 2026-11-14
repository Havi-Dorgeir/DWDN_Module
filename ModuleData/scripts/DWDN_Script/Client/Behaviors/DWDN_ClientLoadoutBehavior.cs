using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;
using DWDN_Script.Common.Duchies;
using DWDN_Script.Common.Network;
using DWDN_Script.Client.UI;
using DWDN_Script.Server.Behaviors;

namespace DWDN_Script.Client.Behaviors
{
    /// <summary>
    /// КЛИЕНТСКАЯ ЧАСТЬ:
    /// Полностью заменяет стандартный экран выбора класса (MissionGauntletClassLoadout)
    /// СТРОГО для фракции DWDN (Даевден). Для остальных ванильных фракций не вмешивается.
    /// </summary>
    public class DWDN_ClientLoadoutBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public static DWDN_ClientLoadoutBehavior Instance { get; private set; }

        public DWDN_LoadoutSelection CurrentSelection { get; private set; }

        private GauntletLayer _gauntletLayer;
        private DWDN_ClassSelectionVM _selectionVM;
        private MissionScreen _missionScreen;

        private MissionLobbyEquipmentNetworkComponent _equipmentNetworkComponent;
        private MultiplayerTeamSelectComponent _teamSelectComponent;
        private MultiplayerMissionAgentVisualSpawnComponent _agentVisualSpawnComponent;
        private MissionMultiplayerGameModeBaseClient _gameModeClient;

        private bool _tryToInitialize;
        private bool _isActive;
        private bool _isTeamSelectOpen;

        public bool IsScreenOpen => _isActive && _gauntletLayer != null;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Instance = this;

            var banreich = DWDN_DuchyManager.GetDuchy("banreich");
            if (banreich != null && banreich.Classes.Count > 0)
            {
                CurrentSelection = banreich.Classes[0].CreateDefaultLoadout(banreich.Id);
            }
            else
            {
                CurrentSelection = new DWDN_LoadoutSelection();
            }
        }

        public override void AfterStart()
        {
            base.AfterStart();

            _equipmentNetworkComponent = Mission?.GetMissionBehavior<MissionLobbyEquipmentNetworkComponent>();
            _teamSelectComponent = Mission?.GetMissionBehavior<MultiplayerTeamSelectComponent>();
            _agentVisualSpawnComponent = Mission?.GetMissionBehavior<MultiplayerMissionAgentVisualSpawnComponent>();
            _gameModeClient = Mission?.GetMissionBehavior<MissionMultiplayerGameModeBaseClient>();

            if (_equipmentNetworkComponent != null)
            {
                _equipmentNetworkComponent.OnToggleLoadout += OnTryToggle;
            }

            if (_teamSelectComponent != null)
            {
                _teamSelectComponent.OnSelectingTeam += OnSelectingTeam;
            }

            if (_agentVisualSpawnComponent != null)
            {
                _agentVisualSpawnComponent.OnMyAgentVisualSpawned += OnMyAgentVisualSpawned;
                _agentVisualSpawnComponent.OnMyAgentSpawnedFromVisual += OnMyAgentSpawnedFromVisual;
                _agentVisualSpawnComponent.OnMyAgentVisualRemoved += OnMyAgentVisualRemoved;
            }

            MissionPeer.OnTeamChanged += OnTeamChanged;
        }

        public override void OnRemoveBehavior()
        {
            UnregisterEvents();
            OnToggled(false);

            if (Instance == this)
            {
                Instance = null;
            }
            base.OnRemoveBehavior();
        }

        protected override void OnEndMission()
        {
            UnregisterEvents();
            OnToggled(false);
            base.OnEndMission();
        }

        private void UnregisterEvents()
        {
            if (_equipmentNetworkComponent != null)
            {
                _equipmentNetworkComponent.OnToggleLoadout -= OnTryToggle;
                _equipmentNetworkComponent = null;
            }

            if (_teamSelectComponent != null)
            {
                _teamSelectComponent.OnSelectingTeam -= OnSelectingTeam;
                _teamSelectComponent = null;
            }

            if (_agentVisualSpawnComponent != null)
            {
                _agentVisualSpawnComponent.OnMyAgentVisualSpawned -= OnMyAgentVisualSpawned;
                _agentVisualSpawnComponent.OnMyAgentSpawnedFromVisual -= OnMyAgentSpawnedFromVisual;
                _agentVisualSpawnComponent.OnMyAgentVisualRemoved -= OnMyAgentVisualRemoved;
                _agentVisualSpawnComponent = null;
            }

            MissionPeer.OnTeamChanged -= OnTeamChanged;
        }

        /// <summary>
        /// Проверяет, играет ли локальный игрок за фракцию DWDN (Даевден).
        /// Если игрок играет за любую другую фракцию (Вландия, Баттания и т.д.), кастомный селектор НЕ активируется!
        /// </summary>
        public bool IsLocalPlayerDwdn()
        {
            if (!GameNetwork.IsMyPeerReady) return false;
            MissionPeer myPeer = GameNetwork.MyPeer?.GetComponent<MissionPeer>();
            if (myPeer == null || myPeer.Team == null) return false;

            // 1. Culture of peer
            if (myPeer.Culture != null && string.Equals(myPeer.Culture.StringId, "dwdn", StringComparison.OrdinalIgnoreCase))
                return true;

            // 2. Culture of team
            string cultureStr = "";
            if (myPeer.Team.Side == BattleSideEnum.Attacker)
            {
                cultureStr = MultiplayerOptions.OptionType.CultureTeam1.GetStrValue();
            }
            else if (myPeer.Team.Side == BattleSideEnum.Defender)
            {
                cultureStr = MultiplayerOptions.OptionType.CultureTeam2.GetStrValue();
            }

            if (string.Equals(cultureStr, "dwdn", StringComparison.OrdinalIgnoreCase))
                return true;

            // 3. MPHeroClass culture
            var heroClass = MultiplayerClassDivisions.GetMPHeroClassForPeer(myPeer);
            if (heroClass?.Culture != null && string.Equals(heroClass.Culture.StringId, "dwdn", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private void UpdateVanillaClassLoadoutVisibility(bool isDwdn)
        {
            if (Mission == null) return;

            try
            {
                foreach (var behavior in Mission.MissionBehaviors)
                {
                    if (behavior != null && behavior.GetType().Name == "MissionGauntletClassLoadout")
                    {
                        FieldInfo glField = behavior.GetType().GetField("_gauntletLayer", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (glField != null && glField.GetValue(behavior) is GauntletLayer gl && gl.UIContext?.Root != null)
                        {
                            gl.UIContext.Root.IsVisible = !isDwdn;
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Client ERROR] UpdateVanillaClassLoadoutVisibility: {ex}");
            }
        }

        private MissionScreen GetMissionScreen()
        {
            if (_missionScreen != null) return _missionScreen;
            _missionScreen = ScreenManager.TopScreen as MissionScreen ?? MissionState.Current?.Handler as MissionScreen;
            return _missionScreen;
        }

        private void OnTeamChanged(NetworkCommunicator peer, Team previousTeam, Team newTeam)
        {
            if (peer != null && peer.IsMine && newTeam != null && (newTeam.IsAttacker || newTeam.IsDefender))
            {
                _isTeamSelectOpen = false;

                if (!IsLocalPlayerDwdn())
                {
                    // Если игрок выбрал команду другой фракции, закрываем наш экран
                    if (_isActive)
                    {
                        OnToggled(false);
                    }
                    return;
                }

                if (_isActive)
                {
                    OnTryToggle(false);
                }
                OnTryToggle(true);
            }
        }

        private void OnSelectingTeam(List<Team> disableTeams)
        {
            _isTeamSelectOpen = true;
            OnTryToggle(false);
        }

        private void OnMyAgentVisualSpawned()
        {
            if (!IsLocalPlayerDwdn()) return;

            _isTeamSelectOpen = false;
            ApplyLoadoutToPedestalPreview(CurrentSelection);
            ApplyAndSendLoadout(CurrentSelection);

            if (!_isActive)
            {
                OnTryToggle(true);
            }
        }

        private void OnMyAgentSpawnedFromVisual()
        {
            OnTryToggle(false);
            if (Agent.Main != null && Agent.Main.IsActive())
            {
                try
                {
                    Agent.Main.Equipment?.CheckLoadedAmmos();
                    Agent.Main.UpdateWeapons();
                    Agent.Main.WieldInitialWeapons();

                    if (Agent.Main.GetOffhandWieldedItemIndex() == EquipmentIndex.None && !Agent.Main.Equipment[EquipmentIndex.Weapon1].IsEmpty)
                    {
                        var offhand = Agent.Main.Equipment[EquipmentIndex.Weapon1];
                        if (offhand.Item?.ItemType == ItemObject.ItemTypeEnum.Banner || offhand.Item?.ItemType == ItemObject.ItemTypeEnum.Shield || offhand.Item?.ItemFlags.HasAnyFlag(ItemFlags.HeldInOffHand) == true)
                        {
                            Agent.Main.TryToWieldWeaponInSlot(EquipmentIndex.Weapon1, Agent.WeaponWieldActionType.Instant, true);
                        }
                    }
                }
                catch {}
            }
        }

        private void OnMyAgentVisualRemoved()
        {
            OnTryToggle(false);
        }

        public void OnTryToggle(bool isActive)
        {
            if (!IsLocalPlayerDwdn())
            {
                UpdateVanillaClassLoadoutVisibility(false);
                if (_isActive)
                {
                    OnToggled(false);
                }
                return;
            }

            if (isActive)
            {
                _tryToInitialize = true;
            }
            else
            {
                _tryToInitialize = false;
                OnToggled(false);
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (Mission == null || Mission.CurrentState != Mission.State.Continuing)
            {
                if (_isActive)
                {
                    OnToggled(false);
                }
                return;
            }

            // Проверяем принадлежность к фракции DWDN
            if (!IsLocalPlayerDwdn())
            {
                UpdateVanillaClassLoadoutVisibility(false);
                if (_isActive)
                {
                    OnToggled(false);
                }
                return;
            }

            UpdateVanillaClassLoadoutVisibility(true);

            MissionPeer myPeer = GameNetwork.IsMyPeerReady ? GameNetwork.MyPeer.GetComponent<MissionPeer>() : null;

            // Если игрок уже заспавнился живым агентом — экран выбора закрыт
            if ((myPeer != null && myPeer.ControlledAgent != null) || (Agent.Main != null && Agent.Main.IsActive()))
            {
                if (_isActive)
                {
                    _tryToInitialize = false;
                    OnToggled(false);
                    return;
                }
            }

            // Открываем экран, когда превью персонажа на пьедестале готов
            if (_tryToInitialize && GameNetwork.IsMyPeerReady && myPeer != null && myPeer.HasSpawnedAgentVisuals && !_isTeamSelectOpen)
            {
                if (OnToggled(true))
                {
                    _tryToInitialize = false;
                }
            }

            // Обновляем текст статуса спавна внизу экрана
            if (_isActive && _selectionVM != null)
            {
                if (_gameModeClient != null && _gameModeClient.IsInWarmup)
                {
                    _selectionVM.SpawnStatusText = "Разминка: выберите снаряжение (Нажмите [F] для спавна)";
                }
                else
                {
                    _selectionVM.SpawnStatusText = "Выберите класс и снаряжение (Нажмите [F] для готовности)";
                }
            }
        }

        private bool OnToggled(bool isActive)
        {
            if (_isActive == isActive)
                return true;

            MissionScreen screen = GetMissionScreen();
            if (screen == null && isActive)
                return false;

            try
            {
                if (screen != null)
                {
                    screen.SetDisplayDialog(isActive);
                }

                if (isActive)
                {
                    _selectionVM = new DWDN_ClassSelectionVM(CurrentSelection, OnSelectionChanged);

                    _gauntletLayer = new GauntletLayer("DWDN_ClassSelectionLayer", 20, false);
                    _gauntletLayer.LoadMovie("DWDN_ClassSelection", _selectionVM);
                    _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.Mouse);

                    screen.AddLayer(_gauntletLayer);
                    _isActive = true;

                    ApplyLoadoutToPedestalPreview(CurrentSelection);
                    ApplyAndSendLoadout(CurrentSelection);
                    Debug.Print("[DWDN Client] Кастомный экран DWDN открыт.");
                }
                else
                {
                    if (_gauntletLayer != null)
                    {
                        _gauntletLayer.InputRestrictions.ResetInputRestrictions();
                        if (screen != null)
                        {
                            screen.RemoveLayer(_gauntletLayer);
                        }
                        else if (ScreenManager.TopScreen != null)
                        {
                            ScreenManager.TopScreen.RemoveLayer(_gauntletLayer);
                        }
                        _gauntletLayer = null;
                    }

                    if (_selectionVM != null)
                    {
                        _selectionVM.OnFinalize();
                        _selectionVM = null;
                    }

                    _isActive = false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Client ERROR] OnToggled({isActive}): {ex}");
                return false;
            }
        }

        private void OnSelectionChanged(DWDN_LoadoutSelection selection, bool classChanged)
        {
            if (selection == null) return;

            CurrentSelection = selection.Clone();

            // 1. Синхронизируем класс с сервером и нативной системой
            if (classChanged)
            {
                SyncNativeTroopIndex(CurrentSelection);
            }

            // 2. Сразу обновляем 3D-персонажа на пьедестале
            ApplyLoadoutToPedestalPreview(CurrentSelection);

            // 3. Отправляем выбор экипировки на сервер
            ApplyAndSendLoadout(CurrentSelection);
        }

        /// <summary>
        /// Синхронизирует выбранный класс с нативным индексом деления (MPHeroClass).
        /// </summary>
        private void SyncNativeTroopIndex(DWDN_LoadoutSelection selection)
        {
            try
            {
                if (!GameNetwork.IsMyPeerReady) return;
                MissionPeer myPeer = GameNetwork.MyPeer.GetComponent<MissionPeer>();
                if (myPeer == null || myPeer.Team == null) return;

                var duchy = DWDN_DuchyManager.GetDuchy(selection.DuchyId);
                var cls = duchy?.GetClass(selection.ClassId);
                if (cls == null) return;

                var allHeroClasses = MultiplayerClassDivisions.GetMPHeroClasses();
                if (allHeroClasses == null) return;

                MultiplayerClassDivisions.MPHeroClass targetHeroClass = null;

                // Точный поиск среди всех классов мультиплеера
                foreach (var hc in allHeroClasses)
                {
                    if (hc == null) continue;

                    if (string.Equals(hc.StringId, cls.Id, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(hc.StringId, cls.TroopId, StringComparison.OrdinalIgnoreCase) ||
                        (hc.HeroCharacter != null && string.Equals(hc.HeroCharacter.StringId, cls.TroopId, StringComparison.OrdinalIgnoreCase)) ||
                        (hc.TroopCharacter != null && string.Equals(hc.TroopCharacter.StringId, cls.TroopId, StringComparison.OrdinalIgnoreCase)))
                    {
                        targetHeroClass = hc;
                        break;
                    }
                }

                if (targetHeroClass != null && targetHeroClass.Culture != null)
                {
                    var cultureClasses = MultiplayerClassDivisions.GetMPHeroClasses(targetHeroClass.Culture).ToList();
                    int foundIndex = cultureClasses.IndexOf(targetHeroClass);
                    if (foundIndex >= 0)
                    {
                        myPeer.NextSelectedTroopIndex = foundIndex;
                        _equipmentNetworkComponent?.EquipmentUpdated();
                        Debug.Print($"[DWDN Client] Переключен нативный класс: {targetHeroClass.StringId} (Index={foundIndex})");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Client ERROR] SyncNativeTroopIndex: {ex}");
            }
        }

        /// <summary>
        /// Мгновенно обновляет модель и экипировку 3D-персонажа на пьедестале.
        /// </summary>
        private void ApplyLoadoutToPedestalPreview(DWDN_LoadoutSelection selection)
        {
            try
            {
                if (selection == null || !GameNetwork.IsMyPeerReady) return;
                MissionPeer myPeer = GameNetwork.MyPeer.GetComponent<MissionPeer>();
                if (myPeer == null || !myPeer.HasSpawnedAgentVisuals) return;

                IAgentVisual agentVisual = myPeer.GetAgentVisualForPeer(0);
                if (agentVisual == null) return;

                AgentVisualsData data = agentVisual.GetCopyAgentVisualsData();
                if (data == null) return;

                var duchy = DWDN_DuchyManager.GetDuchy(selection.DuchyId);
                var cls = duchy?.GetClass(selection.ClassId);

                BasicCharacterObject character = !string.IsNullOrEmpty(cls?.TroopId)
                    ? MBObjectManager.Instance?.GetObject<BasicCharacterObject>(cls.TroopId)
                    : null;

                if (myPeer?.Peer != null)
                {
                    data.BodyProperties(myPeer.Peer.BodyProperties);
                }
                else if (character != null)
                {
                    data.BodyProperties(character.GetBodyProperties(character.Equipment));
                }

                Equipment baseEquip = character != null ? new Equipment(character.Equipment) : (agentVisual.GetEquipment() ?? new Equipment());
                Equipment customEquip = BuildEquipmentFromSelection(selection, baseEquip);

                data.Equipment(customEquip);
                data.RightWieldedItemIndex(0);
                data.LeftWieldedItemIndex(1);
                agentVisual.Refresh(false, data, false);
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Client ERROR] ApplyLoadoutToPedestalPreview: {ex}");
            }
        }

        private static Equipment BuildEquipmentFromSelection(DWDN_LoadoutSelection selection, Equipment baseEquip)
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
                    equip[index] = new EquipmentElement(item);
                }
            }

            SetSlot(EquipmentIndex.Head, selection.HeadId);
            SetSlot(EquipmentIndex.Cape, selection.CapeId);
            SetSlot(EquipmentIndex.Body, selection.BodyId);
            SetSlot(EquipmentIndex.Gloves, selection.GlovesId);
            SetSlot(EquipmentIndex.Leg, selection.LegsId);

            SetSlot(EquipmentIndex.WeaponItemBeginSlot + 0, selection.Weapon0Id);
            SetSlot(EquipmentIndex.WeaponItemBeginSlot + 1, selection.Weapon1Id);
            SetSlot(EquipmentIndex.WeaponItemBeginSlot + 2, selection.Weapon2Id);
            SetSlot(EquipmentIndex.WeaponItemBeginSlot + 3, selection.Weapon3Id);

            // Визуал человека на пьедестале никогда не должен содержать меши лошади
            equip[EquipmentIndex.Horse] = EquipmentElement.Invalid;
            equip[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;

            return equip;
        }

        public void ApplyAndSendLoadout(DWDN_LoadoutSelection selection)
        {
            if (selection == null) return;

            CurrentSelection = selection.Clone();

            if (GameNetwork.IsClient)
            {
                try
                {
                    GameNetwork.BeginModuleEventAsClient();
                    GameNetwork.WriteMessage(new RequestCustomLoadoutMessage(CurrentSelection));
                    GameNetwork.EndModuleEventAsClient();
                }
                catch (Exception ex)
                {
                    Debug.Print($"[DWDN Client ERROR] Ошибка отправки лодаута на сервер: {ex}");
                }
            }
            else
            {
                DWDN_ServerLoadoutBehavior.SetLocalPlayerLoadout(CurrentSelection);
            }
        }
    }
}
