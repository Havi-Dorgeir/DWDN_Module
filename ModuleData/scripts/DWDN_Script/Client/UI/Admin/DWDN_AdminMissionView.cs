using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer.GauntletUI.Mission;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using DWDN_Script.Common.Network;

namespace DWDN_Script.Client.UI.Admin
{
    /// <summary>
    /// Клиентский MissionView кастомного Admin Tools.
    /// Переопределяет стандартный MissionGauntletAdminPanel через TaleWorlds [OverrideView].
    /// Также активируется горячей клавишей [F10] или [Insert] в любой момент миссии.
    /// </summary>
    [OverrideView(typeof(MissionGauntletAdminPanel))]
    public class DWDN_AdminMissionView : MissionView
    {
        public static DWDN_AdminMissionView Instance { get; private set; }

        private GauntletLayer _gauntletLayer;
        private GauntletMovieIdentifier _movie;
        private DWDN_AdminPanelVM _dataSource;
        private bool _isFinalized;
        private static bool _clientIsAdmin = false;
        private bool _handlersRegistered = false;

        public static bool ClientIsAdmin => _clientIsAdmin;

        public DWDN_AdminMissionView()
        {
            ViewOrderPriority = 52;
            Instance = this;
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Instance = this;
            _isFinalized = false;
            EnsureNetworkHandlers();
            CheckAdminStatus();
        }

        public override void EarlyStart()
        {
            base.EarlyStart();
            Instance = this;
            EnsureNetworkHandlers();
            CheckAdminStatus();
        }

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            Instance = this;
            _isFinalized = false;
            EnsureNetworkHandlers();
            CheckAdminStatus();
        }

        private void CheckAdminStatus()
        {
            if (GameNetwork.MyPeer != null && GameNetwork.MyPeer.IsAdmin)
            {
                _clientIsAdmin = true;
            }
        }

        private void EnsureNetworkHandlers()
        {
            if (!_handlersRegistered)
            {
                DWDN_NetworkHelper.RegisterServerMessageHandler<SyncAdminStateMessage>(OnSyncAdminStateMessage);
                _handlersRegistered = true;
            }
        }

        public override void OnMissionScreenFinalize()
        {
            _isFinalized = true;
            ClosePanel();
            if (_handlersRegistered)
            {
                DWDN_NetworkHelper.UnregisterServerMessageHandler<SyncAdminStateMessage>(OnSyncAdminStateMessage);
                _handlersRegistered = false;
            }
            if (Instance == this) Instance = null;
            base.OnMissionScreenFinalize();
        }

        public override void OnRemoveBehavior()
        {
            _isFinalized = true;
            ClosePanel();
            if (_handlersRegistered)
            {
                DWDN_NetworkHelper.UnregisterServerMessageHandler<SyncAdminStateMessage>(OnSyncAdminStateMessage);
                _handlersRegistered = false;
            }
            if (Instance == this) Instance = null;
            base.OnRemoveBehavior();
        }

        private void OnSyncAdminStateMessage(SyncAdminStateMessage message)
        {
            if (message != null)
            {
                bool wasAdmin = _clientIsAdmin;
                _clientIsAdmin = message.IsAdmin;
                Debug.Print($"[DWDN Admin] Client admin state updated: {_clientIsAdmin}");

                if (_clientIsAdmin)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[DWDN Admin Tools] Авторизация успешна! Нажмите [F10] или [Insert] для открытия меню.",
                        Colors.Green
                    ));
                }
                else
                {
                    ClosePanel();
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[DWDN Admin Tools] Права администратора отозваны.",
                        Colors.Red
                    ));
                }
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            TickHotkeys(dt);
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            TickHotkeys(dt);
        }

        private void TickHotkeys(float dt)
        {
            if (_isFinalized) return;

            // Горячие клавиши F10 или Insert для вызова админ панели
            if (Input.IsKeyPressed(InputKey.F10) || Input.IsKeyPressed(InputKey.Insert))
            {
                if (CanOpenAdminPanel())
                {
                    TogglePanel();
                }
                else
                {
                    PromptForAdminPassword();
                }
            }
            else if (_gauntletLayer != null)
            {
                if (Input.IsKeyReleased(InputKey.Escape))
                {
                    ClosePanel();
                }
                else
                {
                    _dataSource?.Tick(dt);
                }
            }
        }

        public void PromptForAdminPassword()
        {
            InformationManager.ShowTextInquiry(new TextInquiryData(
                "DWDN Admin Tools — Авторизация",
                "Введите пароль администратора для получения доступа к инструментам:",
                true,
                true,
                "Войти",
                "Отмена",
                (password) =>
                {
                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        SendLoginRequest(password.Trim());
                    }
                },
                null,
                false,
                (text) => new Tuple<bool, string>(!string.IsNullOrWhiteSpace(text), "")
            ));
        }

        public void SendLoginRequest(string password)
        {
            try
            {
                GameNetwork.BeginModuleEventAsClient();
                GameNetwork.WriteMessage(new RequestAdminActionMessage("login", "", password));
                GameNetwork.EndModuleEventAsClient();
                InformationManager.DisplayMessage(new InformationMessage(
                    "[DWDN Admin] Запрос на авторизацию отправлен на сервер...",
                    Colors.Cyan
                ));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[DWDN Admin Error] {ex.Message}",
                    Colors.Red
                ));
            }
        }

        public bool CanOpenAdminPanel()
        {
            if (GameNetwork.IsServer) return true;
            if (GameNetwork.MyPeer != null && GameNetwork.MyPeer.IsAdmin) return true;
            return _clientIsAdmin;
        }

        public void TogglePanel()
        {
            if (_gauntletLayer == null)
            {
                OpenPanel();
            }
            else
            {
                ClosePanel();
            }
        }

        public void OpenPanel()
        {
            try
            {
                MissionScreen screen = MissionScreen ?? (ScreenManager.TopScreen as MissionScreen);
                _dataSource = new DWDN_AdminPanelVM(this);

                if (screen != null)
                {
                    _gauntletLayer = new GauntletLayer("DWDN_AdminPanel", ViewOrderPriority);
                    _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.Mouse);
                    _movie = _gauntletLayer.LoadMovie("DWDN_AdminPanel", _dataSource);

                    if (_movie != null)
                    {
                        screen.AddLayer(_gauntletLayer);
                        ScreenManager.TrySetFocus(_gauntletLayer);
                        return;
                    }
                    else
                    {
                        Debug.Print("[DWDN Admin] Gauntlet XML 'DWDN_AdminPanel' not found/loaded. Using native inquiry.");
                    }
                }

                // Fallback на нативное TaleWorlds меню с кнопками
                ShowNativeAdminInquiry();
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Admin] Failed to load Gauntlet layer: {ex.Message}. Opening native inquiry fallback.");
                ShowNativeAdminInquiry();
            }
        }

        public void ClosePanel()
        {
            if (_gauntletLayer != null)
            {
                MissionScreen screen = MissionScreen ?? (ScreenManager.TopScreen as MissionScreen);
                _gauntletLayer.InputRestrictions.ResetInputRestrictions();
                if (_movie != null)
                {
                    _gauntletLayer.ReleaseMovie(_movie);
                    _movie = null;
                }
                screen?.RemoveLayer(_gauntletLayer);
                _gauntletLayer = null;
                _dataSource?.OnFinalize();
                _dataSource = null;
            }
        }

        /// <summary>
        /// Встроенный нативный TaleWorlds MultiSelectionInquiry диалог.
        /// Гарантирует 100% работу админ-панели даже при любых несовместимостях Gauntlet UI.
        /// </summary>
        public void ShowNativeAdminInquiry()
        {
            if (_dataSource == null)
            {
                _dataSource = new DWDN_AdminPanelVM(this);
            }

            List<InquiryElement> list = new List<InquiryElement>
            {
                new InquiryElement("heal_me", "Исцелить себя", null, true, "Восстановить HP, боезапас и щит."),
                new InquiryElement("god_me", "Режим Бога (себе)", null, true, "Включить / выключить бессмертие."),
                new InquiryElement("heal_all", "Исцелить всех игроков", null, true, "Исцелить всех живых бойцов обеих команд."),
                new InquiryElement("bring_all", "Телепортировать всех к себе", null, true, "Переместить всех игроков перед администратором."),
                new InquiryElement("kill_bots", "Уничтожить всех ботов", null, true, "Удалить ботов с поля боя."),
                new InquiryElement("end_warmup", "Завершить разминку", null, true, "Мгновенно начать основной раунд."),
                new InquiryElement("extend_warmup", "Продлить разминку", null, true, "Сбросить таймер разминки."),
                new InquiryElement("end_round", "Завершить раунд/матч", null, true, "Завершить текущий раунд."),
                new InquiryElement("players_menu", "Управление игроками...", null, true, "Выбрать игрока для индивидуальных действий (ТП, кик, бан, разоружение).")
            };

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "DWDN Admin Tools",
                "Выберите административное действие:",
                list,
                true,
                1,
                1,
                "Применить",
                "Закрыть",
                (selected) =>
                {
                    string id = selected?.FirstOrDefault()?.Identifier as string;
                    if (id == "players_menu")
                    {
                        ShowNativePlayersInquiry();
                    }
                    else if (!string.IsNullOrEmpty(id))
                    {
                        if (id == "heal_me") _dataSource?.SendAction("heal", "me");
                        else if (id == "god_me") _dataSource?.SendAction("god", "me");
                        else _dataSource?.SendAction(id);
                    }
                },
                null,
                "",
                true));
        }

        public void ShowNativePlayersInquiry()
        {
            List<InquiryElement> players = new List<InquiryElement>();
            if (GameNetwork.NetworkPeers != null)
            {
                foreach (var peer in GameNetwork.NetworkPeers)
                {
                    if (peer == null || peer.IsServerPeer) continue;
                    string id = peer.VirtualPlayer?.Id.ToString() ?? "-";
                    string desc = $"Ping: {(int)peer.AveragePingInMilliseconds}ms | {(peer.ControlledAgent?.IsActive() == true ? "Жив" : "Мертв")}";
                    players.Add(new InquiryElement(id, $"{peer.UserName} (ID: {id})", null, true, desc));
                }
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "DWDN Admin - Выбор Игрока",
                "Выберите игрока для применения санкции/действия:",
                players,
                true,
                1,
                1,
                "Далее",
                "Назад",
                (selected) =>
                {
                    string targetId = selected?.FirstOrDefault()?.Identifier as string;
                    if (!string.IsNullOrEmpty(targetId))
                    {
                        ShowNativePlayerActionsInquiry(targetId);
                    }
                },
                (cancelled) => ShowNativeAdminInquiry(),
                "",
                true));
        }

        public void ShowNativePlayerActionsInquiry(string targetId)
        {
            List<InquiryElement> actions = new List<InquiryElement>
            {
                new InquiryElement("goto", "Телепортироваться к нему", null, true, "Переместиться к позиции игрока."),
                new InquiryElement("bring", "Призвать игрока к себе", null, true, "Переместить игрока перед собой."),
                new InquiryElement("heal", "Исцелить игрока", null, true, "Восстановить HP и боезапас."),
                new InquiryElement("god", "Переключить Режим Бога", null, true, "Сделать игрока бессмертным / смертным."),
                new InquiryElement("freeze", "Заморозить / Разморозить", null, true, "Остановить передвижение и атаку."),
                new InquiryElement("disarm", "Разоружить игрока", null, true, "Выбить всё оружие из слотов."),
                new InquiryElement("slay", "Уничтожить игрока (Slay)", null, true, "Мгновенно убить агента игрока."),
                new InquiryElement("kick", "Исключить (Kick)", null, true, "Отключить игрока с сервера."),
                new InquiryElement("ban", "Заблокировать (Ban)", null, true, "Внести PlayerID в черный список.")
            };

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                $"Действия над целью {targetId}",
                "Выберите санкцию:",
                actions,
                true,
                1,
                1,
                "Выполнить",
                "Отмена",
                (selected) =>
                {
                    string act = selected?.FirstOrDefault()?.Identifier as string;
                    if (!string.IsNullOrEmpty(act))
                    {
                        _dataSource?.SendAction(act, targetId);
                        InformationManager.DisplayMessage(new InformationMessage($"[DWDN Admin] Команда {act} отправлена для {targetId}"));
                    }
                },
                (cancelled) => ShowNativePlayersInquiry(),
                "",
                false));
        }
    }
}
