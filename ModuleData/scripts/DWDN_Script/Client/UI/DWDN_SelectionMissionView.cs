using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.DotNet;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;
using DWDN_Script.Common.Duchies;
using DWDN_Script.Common.Network;
using DWDN_Script.Server.Behaviors;

namespace DWDN_Script.Client.UI
{
    /// <summary>
    /// Автономный экран выбора класса и экипировки DWDN (архитектура LOTR / OMEE).
    /// Является полноправным MissionView с ViewOrderPriority = 20.
    /// Полностью заменяет ванильный MissionGauntletClassLoadout.
    /// Управляет постоянным Gauntlet-слоем, реагирует на смерть, смену раунда и смену команд,
    /// и обновляет нативную марионетку (agentVisual) без дублирования моделей и C++ крашей.
    /// </summary>
    public class DWDN_SelectionMissionView : MissionView
    {
        private GauntletLayer _layer;
        private DWDN_ClassSelectionVM _viewModel;

        private DWDN_LoadoutSelection _currentSelection;
        private bool _visible;
        private bool _isLayerAdded;
        private bool _manualOpen;
        private bool _wasAlive;
        private float _deathTime = -1f;
        private bool _roundArmed;
        private float _lastSpawnRequestTime = float.NegativeInfinity;
        private bool _pedestalUpdatePending;
        private float _pedestalUpdateTimer;

        private IRoundComponent _roundComponent;
        private MissionLobbyComponent _lobbyComponent;
        private MissionMultiplayerGameModeBaseClient _gameModeClient;
        private MissionLobbyEquipmentNetworkComponent _equipmentNetworkComponent;
        private MultiplayerMissionAgentVisualSpawnComponent _visualSpawnComponent;

        private const float RotateSpeed = 2.2f;
        private const float SpawnRequestCooldown = 1.0f;

        private MissionMultiplayerGameModeBaseClient GameModeClient
        {
            get
            {
                if (_gameModeClient == null && Mission != null)
                {
                    _gameModeClient = Mission.GetMissionBehavior<MissionMultiplayerGameModeBaseClient>();
                }
                return _gameModeClient;
            }
        }

        public DWDN_SelectionMissionView()
        {
            base.ViewOrderPriority = 20;
        }

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();

            var banreich = DWDN_DuchyManager.GetDuchy("banreich");
            if (banreich != null && banreich.Classes.Count > 0)
            {
                _currentSelection = banreich.Classes[0].CreateDefaultLoadout(banreich.Id);
            }
            else
            {
                _currentSelection = new DWDN_LoadoutSelection();
            }

            _viewModel = new DWDN_ClassSelectionVM(_currentSelection, OnSelectionChanged, ExecuteSpawn);
            _viewModel.IsActive = false;

            _layer = new GauntletLayer("DWDN_ClassSelectionLayer", base.ViewOrderPriority, false);
            _layer.LoadMovie("DWDN_ClassSelection", _viewModel);

            // По архитектуре LOTR: слой добавляется в MissionScreen ОДИН РАЗ при инициализации
            if (MissionScreen != null)
            {
                MissionScreen.AddLayer(_layer);
                _isLayerAdded = true;
            }

            _equipmentNetworkComponent = Mission?.GetMissionBehavior<MissionLobbyEquipmentNetworkComponent>();
            if (_equipmentNetworkComponent != null)
            {
                _equipmentNetworkComponent.OnToggleLoadout += OnToggleLoadout;
            }

            _visualSpawnComponent = Mission?.GetMissionBehavior<MultiplayerMissionAgentVisualSpawnComponent>();
            if (_visualSpawnComponent != null)
            {
                _visualSpawnComponent.OnMyAgentVisualSpawned += OnMyAgentVisualSpawned;
            }

            MissionPeer.OnTeamChanged += OnTeamChanged;

            _visible = false;
            _manualOpen = false;

            SendLoadoutToServer(_currentSelection);

            Debug.Print("[DWDN] Selection MissionView initialized successfully (LOTR lifecycle + single puppet).");
        }

        private void RequestPedestalUpdate(float delay = 0.05f)
        {
            _pedestalUpdatePending = true;
            _pedestalUpdateTimer = delay;
        }

        private void OnTeamChanged(NetworkCommunicator peer, Team previousTeam, Team newTeam)
        {
            if (peer.IsMine && newTeam != null && (newTeam.IsAttacker || newTeam.IsDefender))
            {
                _deathTime = -1f;
                _roundArmed = true;
                SyncNativeTroopIndex(_currentSelection);
                RequestPedestalUpdate(0.1f);
            }
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);

            if (_layer == null || Mission == null)
                return;

            MissionPeer myPeer = GameNetwork.IsMyPeerReady ? GameNetwork.MyPeer?.GetComponent<MissionPeer>() : null;
            bool hasTeam = myPeer != null && myPeer.Team != null && (int)myPeer.Team.Side != -1;

            if (_pedestalUpdatePending)
            {
                _pedestalUpdateTimer -= dt;
                if (_pedestalUpdateTimer <= 0f)
                {
                    _pedestalUpdatePending = false;
                    UpdatePedestalVisual(_currentSelection);
                }
            }

            // Чистая проверка жизни агента, как в LOTR:
            // Не проверяем myPeer.ControlledAgent, потому что в Bannerlord труп/регдолл остается ControlledAgent и IsActive() возвращает true!
            bool isAlive = Mission.MainAgent != null && Mission.MainAgent.IsActive() && Mission.MainAgent.Health > 0;

            UpdateSpawnState(isAlive);

            bool isEnding = IsMissionEnding();
            bool isDwdn = IsLocalPlayerDwdn(myPeer);

            // Переключение меню по клавише M (или U)
            if (hasTeam && isDwdn && !isEnding && (IsKeyPressed(InputKey.M) || IsKeyPressed(InputKey.U)))
            {
                _manualOpen = !_manualOpen;
                if (!_manualOpen && _visible)
                {
                    Hide();
                }
            }

            // Если меню открыто вручную, закрытие по клавише Escape
            if (_visible && _manualOpen && IsKeyPressed(InputKey.Escape))
            {
                _manualOpen = false;
                Hide();
                return;
            }

            // Автоматическое появление меню после смерти / при смене раунда / при отсутствии живого агента
            bool canShowAuto = !isAlive && hasTeam && isDwdn && !IsOnDeathCamera() && CanPrepareLoadout() && !isEnding;
            bool canShow = (canShowAuto || (_manualOpen && isDwdn)) && !isEnding;

            if (canShow && !_visible)
            {
                Show();
            }
            else if (!canShow && _visible && !_manualOpen)
            {
                Hide();
            }

            if (_visible)
            {
                ReleaseKeyboardFocus();

                // Вращение персонажа на A / D
                if (IsKeyDown(InputKey.A) || IsKeyDown(InputKey.Left))
                {
                    RotatePedestal(RotateSpeed * dt);
                }
                if (IsKeyDown(InputKey.D) || IsKeyDown(InputKey.Right))
                {
                    RotatePedestal(-RotateSpeed * dt);
                }

                // Спавн / подтверждение по клавише F
                if (IsKeyPressed(InputKey.F) && Mission.CurrentTime - _lastSpawnRequestTime >= SpawnRequestCooldown)
                {
                    _lastSpawnRequestTime = Mission.CurrentTime;
                    ExecuteSpawn();
                }
            }
        }

        private void ReleaseKeyboardFocus()
        {
            if (_layer != null && (object)ScreenManager.FocusedLayer == _layer)
            {
                ScreenManager.TryLoseFocus(_layer);
            }
        }

        private bool IsKeyDown(InputKey key)
        {
            if (MissionScreen?.SceneLayer != null && MissionScreen.SceneLayer.Input.IsKeyDown(key))
                return true;
            if (_layer != null && _layer.Input.IsKeyDown(key))
                return true;
            return false;
        }

        private bool IsKeyPressed(InputKey key)
        {
            if (MissionScreen?.SceneLayer != null && MissionScreen.SceneLayer.Input.IsKeyPressed(key))
                return true;
            if (_layer != null && _layer.Input.IsKeyPressed(key))
                return true;
            return false;
        }

        private void UpdateSpawnState(bool alive)
        {
            if (alive && !_wasAlive)
            {
                _roundArmed = false;
                _deathTime = -1f;
            }
            else if (!alive && _wasAlive)
            {
                _deathTime = Mission.CurrentTime;
            }
            _wasAlive = alive;

            EnsureRoundHook();
            if (_roundComponent != null && (int)_roundComponent.CurrentRoundState == 1 && !_roundArmed)
            {
                ArmRound();
            }
        }

        private void EnsureRoundHook()
        {
            if (_roundComponent == null)
            {
                MissionMultiplayerGameModeBaseClient gameModeClient = GameModeClient;
                _roundComponent = gameModeClient?.RoundComponent;
                if (_roundComponent != null)
                {
                    _roundComponent.OnRoundStarted += ArmRound;
                }
            }
        }

        private void ArmRound()
        {
            _roundArmed = true;
            _deathTime = -1f;
        }

        private bool CanPrepareLoadout()
        {
            MissionMultiplayerGameModeBaseClient gameModeClient = GameModeClient;
            if (gameModeClient == null || gameModeClient.IsInWarmup || gameModeClient.RoundComponent == null)
            {
                return true;
            }
            return _roundArmed;
        }

        private bool IsOnDeathCamera()
        {
            var gameMode = Mission?.GetMissionBehavior<MissionMultiplayerGameModeBaseClient>();
            if (gameMode != null && gameMode.IsInWarmup)
            {
                return false;
            }

            if (_deathTime >= 0f)
            {
                return Mission.CurrentTime - _deathTime < 2.5f;
            }
            return false;
        }

        private bool IsMissionEnding()
        {
            if (Mission == null || Mission.CurrentState != Mission.State.Continuing)
                return true;

            if (_lobbyComponent == null)
            {
                _lobbyComponent = Mission.GetMissionBehavior<MissionLobbyComponent>();
            }
            if (_lobbyComponent != null && _lobbyComponent.CurrentMultiplayerState == MissionLobbyComponent.MultiplayerGameState.Ending)
                return true;

            return false;
        }

        public bool IsLocalPlayerDwdn(MissionPeer myPeer)
        {
            if (myPeer == null || myPeer.Team == null)
                return false;

            if (myPeer.Culture != null && string.Equals(myPeer.Culture.StringId, "dwdn", StringComparison.OrdinalIgnoreCase))
                return true;

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

            var heroClass = MultiplayerClassDivisions.GetMPHeroClassForPeer(myPeer);
            if (heroClass?.Culture != null && string.Equals(heroClass.Culture.StringId, "dwdn", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private void OnToggleLoadout(bool isActive)
        {
            if (isActive)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        private void OnMyAgentVisualSpawned()
        {
            RequestPedestalUpdate(0.1f);
        }

        private void Show()
        {
            if (_visible) return;

            if (_layer != null && _viewModel != null)
            {
                _viewModel.IsActive = true;
                _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                _visible = true;

                SyncNativeTroopIndex(_currentSelection);
                RequestPedestalUpdate(0.05f);
                SendLoadoutToServer(_currentSelection);

                Debug.Print("[DWDN] Selection UI shown.");
            }
        }

        private void Hide()
        {
            _visible = false;
            _manualOpen = false;
            if (_viewModel != null)
            {
                _viewModel.IsActive = false;
            }
            if (_layer != null)
            {
                _layer.InputRestrictions.ResetInputRestrictions();
                ReleaseKeyboardFocus();
            }
            Debug.Print("[DWDN] Selection UI hidden.");
        }

        private void RotatePedestal(float deltaYaw)
        {
            try
            {
                MissionPeer myPeer = GameNetwork.IsMyPeerReady ? GameNetwork.MyPeer?.GetComponent<MissionPeer>() : null;
                if (myPeer != null && myPeer.HasSpawnedAgentVisuals)
                {
                    var visual = myPeer.GetAgentVisualForPeer(0);
                    if (visual != null)
                    {
                        var frame = visual.GetFrame();
                        frame.rotation.RotateAboutUp(deltaYaw);
                        visual.GetVisuals()?.SetFrame(ref frame);
                    }
                }
            }
            catch {}
        }

        private void OnSelectionChanged(DWDN_LoadoutSelection selection, bool isClassChange)
        {
            if (selection == null) return;
            _currentSelection = selection;

            // Не вызываем SyncNativeTroopIndex здесь! Иначе нативный спавнер TaleWorlds пересоздает
            // марионетку с белым шейдером материализации (белая вспышка). Синхронизируем только при спавне в ExecuteSpawn.
            UpdatePedestalVisual(_currentSelection);
            SendLoadoutToServer(_currentSelection);
        }

        /// <summary>
        /// Мгновенно обновляет модель и экипировку единственного анимированного персонажа на пьедестале.
        /// </summary>
        private void UpdatePedestalVisual(DWDN_LoadoutSelection selection)
        {
            try
            {
                if (selection == null || !GameNetwork.IsMyPeerReady) return;
                MissionPeer myPeer = GameNetwork.MyPeer?.GetComponent<MissionPeer>();
                if (myPeer == null || !myPeer.HasSpawnedAgentVisuals) return;

                IAgentVisual agentVisual = myPeer.GetAgentVisualForPeer(0);
                if (agentVisual == null || agentVisual.GetVisuals() == null) return;

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

                Equipment customEquip = DWDN_PreviewBuilder.BuildEquipment(selection, character);
                if (customEquip == null) return;

                data.Equipment(customEquip);
                customEquip.GetInitialWeaponIndicesToEquip(out var mainHandWeaponIndex, out var offHandWeaponIndex, out var isMainHandNotUsableWithOneHand);
                if (isMainHandNotUsableWithOneHand)
                {
                    offHandWeaponIndex = EquipmentIndex.None;
                }
                data.RightWieldedItemIndex((int)mainHandWeaponIndex);
                data.LeftWieldedItemIndex((int)offHandWeaponIndex);

                // false = мгновенная смена без белой вспышки (isSlow: false)
                agentVisual.Refresh(false, data, false);

                // Возобновляем анимацию покоя (idle), чтобы персонаж дышал и не застывал в статической позе
                ActionIndexCache actionCode = ActionIndexCache.act_walk_idle_unarmed;
                agentVisual.SetAction(in actionCode);
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN] UpdatePedestalVisual error: {ex.Message}");
            }
        }

        private void SyncNativeTroopIndex(DWDN_LoadoutSelection selection)
        {
            try
            {
                if (!GameNetwork.IsMyPeerReady) return;
                MissionPeer myPeer = GameNetwork.MyPeer?.GetComponent<MissionPeer>();
                if (myPeer == null || myPeer.Team == null) return;

                var duchy = DWDN_DuchyManager.GetDuchy(selection.DuchyId);
                var cls = duchy?.GetClass(selection.ClassId);
                if (cls == null) return;

                var allHeroClasses = MultiplayerClassDivisions.GetMPHeroClasses();
                if (allHeroClasses == null) return;

                MultiplayerClassDivisions.MPHeroClass targetHeroClass = null;
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
                        // Если игрок выбрал пеший бой (HorseId пустой), выставляем индекс пехоты 0, чтобы спавн был без коня
                        int finalIndex = string.IsNullOrEmpty(selection.HorseId) ? 0 : foundIndex;
                        myPeer.SelectedTroopIndex = finalIndex;
                        myPeer.NextSelectedTroopIndex = finalIndex;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Client ERROR] SyncNativeTroopIndex: {ex}");
            }
        }

        private void SendLoadoutToServer(DWDN_LoadoutSelection selection)
        {
            if (selection == null) return;

            if (GameNetwork.IsClient)
            {
                try
                {
                    GameNetwork.BeginModuleEventAsClient();
                    GameNetwork.WriteMessage(new RequestCustomLoadoutMessage(selection));
                    GameNetwork.EndModuleEventAsClient();
                }
                catch (Exception ex)
                {
                    Debug.Print($"[DWDN Client ERROR] SendLoadoutToServer: {ex}");
                }
            }
            else
            {
                DWDN_ServerLoadoutBehavior.SetLocalPlayerLoadout(selection);
            }
        }

        public void ExecuteSpawn()
        {
            SyncNativeTroopIndex(_currentSelection);
            SendLoadoutToServer(_currentSelection);
            _manualOpen = false;

            bool isAlive = Mission?.MainAgent != null && Mission.MainAgent.IsActive() && Mission.MainAgent.Health > 0;

            if (isAlive && _visible)
            {
                Hide();
            }
            else if (!isAlive)
            {
                MissionMultiplayerGameModeBaseClient missionBehavior = Mission?.GetMissionBehavior<MissionMultiplayerGameModeBaseClient>();
                MissionMultiplayerGameModeFlagDominationClient flagDomClient = missionBehavior as MissionMultiplayerGameModeFlagDominationClient;
                flagDomClient?.OnRequestForfeitSpawn();
            }

            Debug.Print("[DWDN Client] Игрок подтвердил спавн / выбор экипировки [F].");
        }

        public override void OnMissionScreenFinalize()
        {
            MissionPeer.OnTeamChanged -= OnTeamChanged;

            if (_roundComponent != null)
            {
                _roundComponent.OnRoundStarted -= ArmRound;
                _roundComponent = null;
            }

            if (_equipmentNetworkComponent != null)
            {
                _equipmentNetworkComponent.OnToggleLoadout -= OnToggleLoadout;
                _equipmentNetworkComponent = null;
            }

            if (_visualSpawnComponent != null)
            {
                _visualSpawnComponent.OnMyAgentVisualSpawned -= OnMyAgentVisualSpawned;
                _visualSpawnComponent = null;
            }

            _visible = false;
            _lobbyComponent = null;
            _gameModeClient = null;

            if (_layer != null)
            {
                _layer.InputRestrictions.ResetInputRestrictions();
                if (_isLayerAdded && MissionScreen != null)
                {
                    MissionScreen.RemoveLayer(_layer);
                    _isLayerAdded = false;
                }
                _layer = null;
            }

            if (_viewModel != null)
            {
                _viewModel.OnFinalize();
                _viewModel = null;
            }

            base.OnMissionScreenFinalize();
            Debug.Print("[DWDN] Selection MissionView finalized.");
        }
    }
}