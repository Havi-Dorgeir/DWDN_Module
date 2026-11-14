using System;
using System.Collections.Generic;
using System.Reflection;
using NetworkMessages.FromClient;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using DWDN_Script.Common.Network;

namespace DWDN_Script.Server.Admin
{
    public class DWDN_AdminGameHandler : GameHandler
    {
        public override void OnBeforeSave() { }
        public override void OnAfterSave() { }

        protected override void OnGameNetworkBegin()
        {
            base.OnGameNetworkBegin();
            DWDN_AdminService.Initialize();
            RegisterNetworkHandlers(GameNetwork.NetworkMessageHandlerRegisterer.RegisterMode.Add);
        }

        protected override void OnGameNetworkEnd()
        {
            base.OnGameNetworkEnd();
            RegisterNetworkHandlers(GameNetwork.NetworkMessageHandlerRegisterer.RegisterMode.Remove);
        }

        protected override void OnPlayerConnect(VirtualPlayer peer)
        {
            base.OnPlayerConnect(peer);
            if (peer.Communicator is NetworkCommunicator netPeer)
            {
                DWDN_FieldArtilleryBehavior.SyncEnginesToPeer(netPeer);
                if (DWDN_AdminService.IsAdmin(netPeer))
                {
                    DWDN_AdminService.SetPeerAdmin(netPeer, true);
                    DWDN_AdminService.SendToPeer(netPeer, "Вы распознаны как Администратор сервера.");
                    Debug.Print($"[DWDN Admin] Admin connected: {peer.UserName} (ID: {peer.Id})");
                }
            }
        }

        protected override void OnPlayerDisconnect(VirtualPlayer peer)
        {
            base.OnPlayerDisconnect(peer);
        }

        private void RegisterNetworkHandlers(GameNetwork.NetworkMessageHandlerRegisterer.RegisterMode mode)
        {
            if (GameNetwork.IsServer)
            {
                GameNetwork.NetworkMessageHandlerRegisterer registerer = new GameNetwork.NetworkMessageHandlerRegisterer(mode);
                registerer.Register<PlayerMessageAll>(HandleClientEventPlayerMessageAll);
                registerer.Register<PlayerMessageTeam>(HandleClientEventPlayerMessageTeam);
                registerer.Register<RequestAdminActionMessage>(HandleClientEventRequestAdminAction);
            }
        }

        private bool HandleClientEventRequestAdminAction(NetworkCommunicator peer, RequestAdminActionMessage message)
        {
            if (peer == null || message == null)
                return true;

            // 1. Авторизация по паролю (доступна неавторизованным игрокам)
            if (string.Equals(message.Action, "login", StringComparison.OrdinalIgnoreCase))
            {
                bool success = DWDN_AdminService.TryLogin(peer, message.Argument);
                if (!success)
                {
                    DWDN_AdminService.SendToPeer(peer, "Неверный пароль администратора!");
                }
                return true;
            }

            // 2. Строгая проверка прав администратора
            if (!DWDN_AdminService.IsAdmin(peer))
            {
                if (peer.IsAdmin)
                {
                    // Игрок уже авторизован через нативный adminpassword консоли
                    DWDN_AdminService.SetPeerAdmin(peer, true);
                }
                else
                {
                    Debug.Print($"[DWDN Admin SECURITY WARNING] Unauthorized RequestAdminAction from {peer.UserName} (ID: {peer.VirtualPlayer?.Id}): Action={message.Action}");
                    DWDN_AdminService.SendToPeer(peer, "Доступ запрещен. Вы не являетесь администратором.");
                    return true;
                }
            }

            try
            {
                DWDN_AdminService.ExecuteAction(peer, message.Action, message.TargetPlayerId, message.Argument);
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Admin] Error executing action '{message.Action}': {ex.Message}");
            }

            return true;
        }

        private bool HandleClientEventPlayerMessageAll(NetworkCommunicator peer, PlayerMessageAll message)
        {
            if (peer == null || message == null || string.IsNullOrWhiteSpace(message.Message))
                return true;

            if (message.Message.StartsWith("!"))
            {
                try
                {
                    DWDN_AdminService.HandleCommand(peer, message.Message);

                    // Очищаем список получателей, чтобы серверная команда не отображалась в общем чате
                    typeof(PlayerMessageAll).GetProperty("ReceiverList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.SetValue(message, new List<VirtualPlayer>());
                    typeof(PlayerMessageAll).GetProperty("HasReceiverList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.SetValue(message, true);
                }
                catch (Exception ex)
                {
                    Debug.Print($"[DWDN Admin] Error handling chat command '{message.Message}': {ex.Message}");
                }
            }

            return true;
        }

        private bool HandleClientEventPlayerMessageTeam(NetworkCommunicator peer, PlayerMessageTeam message)
        {
            if (peer == null || message == null || string.IsNullOrWhiteSpace(message.Message))
                return true;

            if (message.Message.StartsWith("!"))
            {
                try
                {
                    DWDN_AdminService.HandleCommand(peer, message.Message);

                    // Очищаем список получателей для команд из командного чата
                    typeof(PlayerMessageTeam).GetProperty("ReceiverList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.SetValue(message, new List<VirtualPlayer>());
                    typeof(PlayerMessageTeam).GetProperty("HasReceiverList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.SetValue(message, true);
                }
                catch (Exception ex)
                {
                    Debug.Print($"[DWDN Admin] Error handling team chat command '{message.Message}': {ex.Message}");
                }
            }

            return true;
        }
    }
}
