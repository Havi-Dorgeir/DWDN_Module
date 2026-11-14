using System;
using System.Reflection;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace DWDN_Script.Common.Network
{
    /// <summary>
    /// Вспомогательный класс для регистрации сетевых обработчиков для Mod-пакетов.
    /// Так как TaleWorlds хранит AddClientMessageHandler как non-public статический метод в GameNetwork,
    /// этот класс обеспечивает безопасную привязку через Reflection.
    /// </summary>
    public static class DWDN_NetworkHelper
    {
        private static readonly MethodInfo _addClientMessageHandler =
            typeof(GameNetwork).GetMethod("AddClientMessageHandler", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo _removeClientMessageHandler =
            typeof(GameNetwork).GetMethod("RemoveClientMessageHandler", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo _addServerMessageHandler =
            typeof(GameNetwork).GetMethod("AddServerMessageHandler", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo _removeServerMessageHandler =
            typeof(GameNetwork).GetMethod("RemoveServerMessageHandler", BindingFlags.NonPublic | BindingFlags.Static);

        public static void RegisterClientMessageHandler<T>(GameNetworkMessage.ClientMessageHandlerDelegate<T> handler)
            where T : GameNetworkMessage
        {
            try
            {
                if (_addClientMessageHandler != null)
                {
                    MethodInfo generic = _addClientMessageHandler.MakeGenericMethod(typeof(T));
                    generic.Invoke(null, new object[] { handler });
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN_NetworkHelper ERROR] RegisterClientMessageHandler failed: {ex}");
            }
        }

        public static void UnregisterClientMessageHandler<T>(GameNetworkMessage.ClientMessageHandlerDelegate<T> handler)
            where T : GameNetworkMessage
        {
            try
            {
                if (_removeClientMessageHandler != null)
                {
                    MethodInfo generic = _removeClientMessageHandler.MakeGenericMethod(typeof(T));
                    generic.Invoke(null, new object[] { handler });
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN_NetworkHelper ERROR] UnregisterClientMessageHandler failed: {ex}");
            }
        }

        public static void RegisterServerMessageHandler<T>(GameNetworkMessage.ServerMessageHandlerDelegate<T> handler)
            where T : GameNetworkMessage
        {
            try
            {
                if (_addServerMessageHandler != null)
                {
                    MethodInfo generic = _addServerMessageHandler.MakeGenericMethod(typeof(T));
                    generic.Invoke(null, new object[] { handler });
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN_NetworkHelper ERROR] RegisterServerMessageHandler failed: {ex}");
            }
        }

        public static void UnregisterServerMessageHandler<T>(GameNetworkMessage.ServerMessageHandlerDelegate<T> handler)
            where T : GameNetworkMessage
        {
            try
            {
                if (_removeServerMessageHandler != null)
                {
                    MethodInfo generic = _removeServerMessageHandler.MakeGenericMethod(typeof(T));
                    generic.Invoke(null, new object[] { handler });
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN_NetworkHelper ERROR] UnregisterServerMessageHandler failed: {ex}");
            }
        }
    }
}
