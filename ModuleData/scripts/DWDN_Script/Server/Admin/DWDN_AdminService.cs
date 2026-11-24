using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Path = System.IO.Path;
using NetworkMessages.FromServer;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.PlayerServices;
using DWDN_Script.Common.Network;

namespace DWDN_Script.Server.Admin
{
    public static class DWDN_AdminService
    {
        private const string AdminPrefix = "[DWDN Admin] ";
        private static readonly HashSet<string> _adminIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<NetworkCommunicator> _authenticatedPeers = new HashSet<NetworkCommunicator>();
        private static readonly HashSet<NetworkCommunicator> _godModePeers = new HashSet<NetworkCommunicator>();
        private static readonly HashSet<NetworkCommunicator> _frozenPeers = new HashSet<NetworkCommunicator>();
        private static readonly Dictionary<NetworkCommunicator, Vec3> _markedPositions = new Dictionary<NetworkCommunicator, Vec3>();

        private static string _adminPassword = null;
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            LoadAdminPassword();
            LoadAdminIds();
        }

        private static void LoadAdminPassword()
        {
            try
            {
                string mpPass = MultiplayerOptions.OptionType.AdminPassword.GetStrValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions);
                if (!string.IsNullOrWhiteSpace(mpPass))
                {
                    _adminPassword = mpPass.Trim();
                    return;
                }
            }
            catch { }

            // Попытка прочесть DWDN_config.txt
            string[] possiblePaths = new string[]
            {
                Path.Combine(BasePath.Name, "Modules", "Native", "DWDN_config.txt"),
                Path.Combine(BasePath.Name, "Modules", "DaewdenM", "DWDN_config.txt"),
                Path.Combine(BasePath.Name, "Modules", "DWDN_TRY", "DWDN_config.txt"),
                Path.Combine(Directory.GetCurrentDirectory(), "Modules", "Native", "DWDN_config.txt")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        foreach (var line in File.ReadAllLines(path))
                        {
                            string trimmed = line.Trim();
                            if (trimmed.StartsWith("AdminPassword", StringComparison.OrdinalIgnoreCase))
                            {
                                string[] parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 2)
                                {
                                    _adminPassword = parts[1].Trim();
                                    return;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        private static void LoadAdminIds()
        {
            _adminIds.Clear();
            string[] possiblePaths = new string[]
            {
                Path.Combine(BasePath.Name, "Modules", "Native", "admins.txt"),
                Path.Combine(BasePath.Name, "Modules", "DaewdenM", "admins.txt"),
                Path.Combine(BasePath.Name, "Modules", "DWDN_TRY", "admins.txt")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        foreach (var line in File.ReadAllLines(path))
                        {
                            string trimmed = line.Trim();
                            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                            int commentIdx = trimmed.IndexOf('#');
                            if (commentIdx >= 0) trimmed = trimmed.Substring(0, commentIdx).Trim();
                            if (!string.IsNullOrEmpty(trimmed))
                            {
                                _adminIds.Add(trimmed);
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        public static bool IsAdmin(NetworkCommunicator peer)
        {
            if (peer == null) return false;
            if (peer.IsServerPeer) return true;
            if (peer.IsAdmin) return true;
            if (_authenticatedPeers.Contains(peer)) return true;

            string id = peer.VirtualPlayer?.Id.ToString();
            if (!string.IsNullOrEmpty(id) && _adminIds.Contains(id))
            {
                return true;
            }

            return false;
        }

        public static void SetPeerAdmin(NetworkCommunicator peer, bool isAdmin)
        {
            if (peer == null) return;
            try
            {
                typeof(NetworkCommunicator).GetProperty("IsAdmin", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetSetMethod(true)?.Invoke(peer, new object[] { isAdmin });
            }
            catch { }

            if (isAdmin)
            {
                _authenticatedPeers.Add(peer);
            }
            else
            {
                _authenticatedPeers.Remove(peer);
            }

            SyncAdminStateToPeer(peer, isAdmin);
        }

        public static void SyncAdminStateToPeer(NetworkCommunicator peer, bool isAdmin)
        {
            if (peer == null) return;
            try
            {
                GameNetwork.BeginModuleEventAsServer(peer);
                GameNetwork.WriteMessage(new SyncAdminStateMessage(isAdmin));
                GameNetwork.EndModuleEventAsServer();
            }
            catch { }
        }

        public static void ExecuteAction(NetworkCommunicator executor, string action, string targetArg, string extraArg)
        {
            if (executor == null || !IsAdmin(executor)) return;
            if (string.IsNullOrWhiteSpace(action)) return;

            string normAction = action.Trim().ToLowerInvariant();
            switch (normAction)
            {
                case "heal":
                    {
                        var targets = ResolveTargets(executor, string.IsNullOrWhiteSpace(targetArg) ? "me" : targetArg);
                        float hp = -1f;
                        if (!string.IsNullOrWhiteSpace(extraArg) && float.TryParse(extraArg, out float parsedHp)) hp = parsedHp;
                        foreach (var t in targets)
                        {
                            HealTarget(t.ControlledAgent, hp);
                        }
                        SendToPeer(executor, $"Исцелено {targets.Count} игроков.");
                    }
                    break;

                case "heal_all":
                    {
                        var targets = ResolveTargets(executor, "all");
                        foreach (var t in targets)
                        {
                            HealTarget(t.ControlledAgent);
                        }
                        SendToPeer(executor, $"Все живые игроки ({targets.Count}) исцелены.");
                    }
                    break;

                case "god":
                case "godmode":
                    {
                        var targets = ResolveTargets(executor, string.IsNullOrWhiteSpace(targetArg) ? "me" : targetArg);
                        foreach (var t in targets)
                        {
                            bool active = ToggleGodMode(t);
                            SendToPeer(executor, $"Режим Бога для {t.UserName}: {(active ? "ВКЛ" : "ВЫКЛ")}.");
                        }
                    }
                    break;

                case "slay":
                case "kill":
                    {
                        var targets = ResolveTargets(executor, targetArg);
                        foreach (var t in targets)
                        {
                            if (t.ControlledAgent != null && t.ControlledAgent.IsActive())
                            {
                                Blow blow = new Blow(t.ControlledAgent.Index)
                                {
                                    DamageType = DamageTypes.Blunt,
                                    BaseMagnitude = 10000f,
                                    InflictedDamage = 10000,
                                    GlobalPosition = t.ControlledAgent.Position
                                };
                                t.ControlledAgent.Die(blow);
                            }
                        }
                        SendToPeer(executor, $"Уничтожено {targets.Count} целей.");
                    }
                    break;

                case "kill_all":
                    {
                        var targets = ResolveTargets(executor, "all");
                        foreach (var t in targets)
                        {
                            if (t != executor && t.ControlledAgent != null && t.ControlledAgent.IsActive())
                            {
                                Blow blow = new Blow(t.ControlledAgent.Index)
                                {
                                    DamageType = DamageTypes.Blunt,
                                    BaseMagnitude = 10000f,
                                    InflictedDamage = 10000,
                                    GlobalPosition = t.ControlledAgent.Position
                                };
                                t.ControlledAgent.Die(blow);
                            }
                        }
                        SendToPeer(executor, $"Все остальные игроки ({targets.Count}) уничтожены.");
                    }
                    break;

                case "kill_bots":
                    {
                        int botCount = 0;
                        if (Mission.Current?.Agents != null)
                        {
                            foreach (var a in Mission.Current.Agents.ToList())
                            {
                                if (a != null && a.IsActive() && !a.IsPlayerControlled && a.IsHuman)
                                {
                                    Blow blow = new Blow(a.Index)
                                    {
                                        DamageType = DamageTypes.Blunt,
                                        BaseMagnitude = 10000f,
                                        InflictedDamage = 10000,
                                        GlobalPosition = a.Position
                                    };
                                    a.Die(blow);
                                    botCount++;
                                }
                            }
                        }
                        SendToPeer(executor, $"Уничтожено ботов: {botCount}.");
                    }
                    break;

                case "freeze":
                    {
                        var targets = ResolveTargets(executor, targetArg);
                        foreach (var t in targets)
                        {
                            bool frozen = ToggleFreeze(t);
                            SendToPeer(executor, $"Заморозка для {t.UserName}: {(frozen ? "ВКЛ" : "ВЫКЛ")}.");
                        }
                    }
                    break;

                case "disarm":
                    {
                        var targets = ResolveTargets(executor, targetArg);
                        foreach (var t in targets)
                        {
                            Disarm(t.ControlledAgent);
                        }
                        SendToPeer(executor, $"Разоружено {targets.Count} игроков.");
                    }
                    break;

                case "goto":
                    {
                        var targets = ResolveTargets(executor, targetArg);
                        if (targets.Count > 0 && targets[0].ControlledAgent != null && targets[0].ControlledAgent.IsActive())
                        {
                            executor.ControlledAgent?.TeleportToPosition(targets[0].ControlledAgent.Position);
                            SendToPeer(executor, $"Вы переместились к {targets[0].UserName}.");
                        }
                        else
                        {
                            SendToPeer(executor, $"Цель '{targetArg}' не найдена.");
                        }
                    }
                    break;

                case "bring":
                    {
                        var targets = ResolveTargets(executor, targetArg);
                        if (targets.Count > 0)
                        {
                            TeleportGroupInFrontOf(executor, targets);
                            SendToPeer(executor, $"Перемещено игроков ({targets.Count}) перед вами.");
                        }
                        else
                        {
                            SendToPeer(executor, $"Цели '{targetArg}' не найдены.");
                        }
                    }
                    break;

                case "bring_all":
                    {
                        var targets = ResolveTargets(executor, "all");
                        TeleportGroupInFrontOf(executor, targets);
                        SendToPeer(executor, $"Все живые игроки ({targets.Count}) перемещены к вам.");
                    }
                    break;

                case "kick":
                    {
                        var targets = ResolveTargets(executor, targetArg);
                        if (targets.Count > 0)
                        {
                            string reason = string.IsNullOrWhiteSpace(extraArg) ? "Kicked by Admin" : extraArg;
                            KickPlayer(targets[0], reason);
                        }
                    }
                    break;

                case "ban":
                    {
                        var targets = ResolveTargets(executor, targetArg);
                        if (targets.Count > 0)
                        {
                            string reason = string.IsNullOrWhiteSpace(extraArg) ? "Banned by Admin" : extraArg;
                            BanPlayer(targets[0], executor, reason);
                        }
                    }
                    break;

                case "ammo":
                    {
                        var targets = ResolveTargets(executor, string.IsNullOrWhiteSpace(targetArg) ? "me" : targetArg);
                        foreach (var t in targets)
                        {
                            RefillAmmo(t.ControlledAgent);
                        }
                        SendToPeer(executor, $"Пополнен боезапас для {targets.Count} игроков.");
                    }
                    break;

                case "repair":
                    {
                        var targets = ResolveTargets(executor, string.IsNullOrWhiteSpace(targetArg) ? "me" : targetArg);
                        foreach (var t in targets)
                        {
                            RepairShields(t.ControlledAgent);
                        }
                        SendToPeer(executor, $"Починены щиты для {targets.Count} игроков.");
                    }
                    break;

                case "end_warmup":
                    EndWarmup();
                    break;

                case "extend_warmup":
                    ExtendWarmup();
                    break;

                case "end_round":
                    EndRoundOrMission();
                    break;

                case "map":
                    if (!string.IsNullOrWhiteSpace(extraArg))
                    {
                        SetNextMap(extraArg);
                    }
                    break;

                case "alert":
                    if (!string.IsNullOrWhiteSpace(extraArg))
                    {
                        BroadcastAlert(extraArg);
                    }
                    break;

                default:
                    // Если передан текст консольной команды
                    HandleCommand(executor, action.StartsWith("!") ? action : ("!" + action + (string.IsNullOrWhiteSpace(targetArg) ? "" : " " + targetArg) + (string.IsNullOrWhiteSpace(extraArg) ? "" : " " + extraArg)));
                    break;
            }
        }

        public static void SendToPeer(NetworkCommunicator peer, string message)
        {
            if (peer == null) return;
            try
            {
                GameNetwork.BeginModuleEventAsServer(peer);
                GameNetwork.WriteMessage(new ServerMessage(AdminPrefix + message));
                GameNetwork.EndModuleEventAsServer();
            }
            catch { }
        }

        public static void SendToAll(string message)
        {
            try
            {
                GameNetwork.BeginBroadcastModuleEvent();
                GameNetwork.WriteMessage(new ServerMessage(AdminPrefix + message));
                GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.IncludeUnsynchronizedClients);
            }
            catch { }
        }

        public static void BroadcastAlert(string message)
        {
            try
            {
                GameNetwork.BeginBroadcastModuleEvent();
                GameNetwork.WriteMessage(new ServerAdminMessage(message, true));
                GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None);
            }
            catch { }
        }

        public static List<NetworkCommunicator> ResolveTargets(NetworkCommunicator executor, string targetStr)
        {
            var result = new List<NetworkCommunicator>();
            if (string.IsNullOrWhiteSpace(targetStr) || targetStr.Equals("me", StringComparison.OrdinalIgnoreCase))
            {
                if (executor != null) result.Add(executor);
                return result;
            }

            if (targetStr.Equals("all", StringComparison.OrdinalIgnoreCase) || targetStr.Equals("*", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var peer in GameNetwork.NetworkPeers)
                {
                    if (peer.IsSynchronized && peer.ControlledAgent != null && peer.ControlledAgent.IsActive())
                    {
                        result.Add(peer);
                    }
                }
                return result;
            }

            if (targetStr.Equals("team1", StringComparison.OrdinalIgnoreCase) || targetStr.Equals("att", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var peer in GameNetwork.NetworkPeers)
                {
                    var mp = peer.GetComponent<MissionPeer>();
                    if (peer.IsSynchronized && mp?.Team != null && mp.Team.Side == BattleSideEnum.Attacker &&
                        peer.ControlledAgent != null && peer.ControlledAgent.IsActive())
                    {
                        result.Add(peer);
                    }
                }
                return result;
            }

            if (targetStr.Equals("team2", StringComparison.OrdinalIgnoreCase) || targetStr.Equals("def", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var peer in GameNetwork.NetworkPeers)
                {
                    var mp = peer.GetComponent<MissionPeer>();
                    if (peer.IsSynchronized && mp?.Team != null && mp.Team.Side == BattleSideEnum.Defender &&
                        peer.ControlledAgent != null && peer.ControlledAgent.IsActive())
                    {
                        result.Add(peer);
                    }
                }
                return result;
            }

            // Поиск по части имени или PlayerId
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.UserName.IndexOf(targetStr, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (peer.VirtualPlayer != null && peer.VirtualPlayer.Id.ToString().IndexOf(targetStr, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    result.Add(peer);
                }
            }

            return result;
        }

        public static void TeleportGroupInFrontOf(NetworkCommunicator anchorPeer, List<NetworkCommunicator> targets)
        {
            if (anchorPeer?.ControlledAgent == null || !anchorPeer.ControlledAgent.IsActive() || targets == null || targets.Count == 0)
                return;

            Agent anchor = anchorPeer.ControlledAgent;
            Vec2 fwd2 = anchor.LookDirection.AsVec2.Normalized();
            if (fwd2.LengthSquared < 0.001f)
            {
                fwd2 = new Vec2(0f, 1f);
            }
            Vec2 right2 = new Vec2(-fwd2.y, fwd2.x);

            Vec3 anchorPos = anchor.Position;
            Vec3 basePos = anchorPos + new Vec3(fwd2.x * 4.0f, fwd2.y * 4.0f, 0f);

            float dx = 1.8f;
            float dy = 2.0f;

            int count = targets.Count;
            int cols = Math.Min(8, Math.Max(3, (int)Math.Ceiling(Math.Sqrt(count * 2.0))));

            int placed = 0;
            foreach (var peer in targets)
            {
                if (peer.ControlledAgent == null || !peer.ControlledAgent.IsActive()) continue;
                if (peer == anchorPeer && targets.Count > 1) continue;

                int row = placed / cols;
                int col = placed % cols;

                float offX = (col - (cols - 1) * 0.5f) * dx;
                float offY = row * dy;

                Vec3 targetPos = basePos + new Vec3(right2.x * offX + fwd2.x * offY, right2.y * offX + fwd2.y * offY, 0f);

                if (Mission.Current?.Scene != null)
                {
                    float groundZ = targetPos.z;
                    if (Mission.Current.Scene.GetHeightAtPoint(targetPos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref groundZ))
                    {
                        targetPos.z = groundZ;
                    }
                }

                peer.ControlledAgent.TeleportToPosition(targetPos);
                placed++;
            }
        }

        public static void HealTarget(Agent agent, float amount = -1f)
        {
            if (agent == null || !agent.IsActive()) return;

            if (amount <= 0f || amount >= agent.HealthLimit)
            {
                agent.Health = agent.HealthLimit;
            }
            else
            {
                agent.Health = Math.Min(agent.HealthLimit, agent.Health + amount);
            }

            if (agent.MountAgent != null && agent.MountAgent.IsActive())
            {
                agent.MountAgent.Health = agent.MountAgent.HealthLimit;
            }

            for (int i = 0; i < 4; i++)
            {
                MissionWeapon mw = agent.Equipment[i];
                if (mw.IsAnyAmmo())
                {
                    agent.SetWeaponAmountInSlot((EquipmentIndex)i, mw.ModifiedMaxAmount, true);
                }
                else if (mw.IsShield())
                {
                    agent.ChangeWeaponHitPoints((EquipmentIndex)i, mw.ModifiedMaxHitPoints);
                }
            }
        }

        public static void RefillAmmo(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return;
            for (int i = 0; i < 4; i++)
            {
                MissionWeapon mw = agent.Equipment[i];
                if (mw.IsAnyAmmo())
                {
                    agent.SetWeaponAmountInSlot((EquipmentIndex)i, mw.ModifiedMaxAmount, true);
                }
            }
        }

        public static void RepairShields(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return;
            for (int i = 0; i < 4; i++)
            {
                MissionWeapon mw = agent.Equipment[i];
                if (mw.IsShield())
                {
                    agent.ChangeWeaponHitPoints((EquipmentIndex)i, mw.ModifiedMaxHitPoints);
                }
            }
        }

        public static bool ToggleGodMode(NetworkCommunicator peer)
        {
            if (peer == null) return false;
            bool newState;
            if (_godModePeers.Contains(peer))
            {
                _godModePeers.Remove(peer);
                newState = false;
                if (peer.ControlledAgent != null && peer.ControlledAgent.IsActive())
                {
                    peer.ControlledAgent.SetMortalityState(Agent.MortalityState.Mortal);
                    if (peer.ControlledAgent.MountAgent != null)
                    {
                        peer.ControlledAgent.MountAgent.SetMortalityState(Agent.MortalityState.Mortal);
                    }
                }
            }
            else
            {
                _godModePeers.Add(peer);
                newState = true;
                if (peer.ControlledAgent != null && peer.ControlledAgent.IsActive())
                {
                    peer.ControlledAgent.SetMortalityState(Agent.MortalityState.Invulnerable);
                    peer.ControlledAgent.Health = peer.ControlledAgent.HealthLimit;
                    if (peer.ControlledAgent.MountAgent != null)
                    {
                        peer.ControlledAgent.MountAgent.SetMortalityState(Agent.MortalityState.Invulnerable);
                        peer.ControlledAgent.MountAgent.Health = peer.ControlledAgent.MountAgent.HealthLimit;
                    }
                }
            }
            return newState;
        }

        public static bool IsGodMode(NetworkCommunicator peer) => _godModePeers.Contains(peer);

        public static bool ToggleFreeze(NetworkCommunicator peer)
        {
            if (peer?.ControlledAgent == null || !peer.ControlledAgent.IsActive()) return false;
            Agent a = peer.ControlledAgent;

            if (_frozenPeers.Contains(peer))
            {
                _frozenPeers.Remove(peer);
                a.SetMaximumSpeedLimit(-1f, false);
                a.IsItemUseDisabled = false;
                if (a.MountAgent != null)
                {
                    a.MountAgent.SetMaximumSpeedLimit(-1f, false);
                }
                return false;
            }
            else
            {
                _frozenPeers.Add(peer);
                a.SetMaximumSpeedLimit(0.0001f, false);
                a.IsItemUseDisabled = true;
                if (a.MountAgent != null)
                {
                    a.MountAgent.SetMaximumSpeedLimit(0.0001f, false);
                }
                return true;
            }
        }

        public static void Disarm(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return;
            try
            {
                var primary = agent.GetPrimaryWieldedItemIndex();
                if (primary != EquipmentIndex.None)
                {
                    agent.DropItem(primary);
                }
                var offhand = agent.GetOffhandWieldedItemIndex();
                if (offhand != EquipmentIndex.None)
                {
                    agent.DropItem(offhand);
                }

                // Также сбрасываем всё экипированное оружие из слотов 0-3
                for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.ExtraWeaponSlot; i++)
                {
                    if (!agent.Equipment[i].IsEmpty)
                    {
                        agent.DropItem(i);
                    }
                }
            }
            catch { }
        }

        public static void Dismount(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return;
            try
            {
                if (agent.MountAgent != null && agent.MountAgent.IsActive())
                {
                    agent.MountAgent.FadeOut(true, true);
                }
            }
            catch { }
        }

        public static void Slay(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return;
            try
            {
                agent.FadeOut(true, true);
            }
            catch { }
        }

        public static bool KickPlayer(NetworkCommunicator peer, string reason)
        {
            if (peer == null || peer.IsServerPeer) return false;
            try
            {
                DisconnectInfo disconnectInfo = peer.PlayerConnectionInfo?.GetParameter<DisconnectInfo>("DisconnectInfo") ?? new DisconnectInfo();
                disconnectInfo.Type = DisconnectType.KickedByHost;
                peer.PlayerConnectionInfo?.AddParameter("DisconnectInfo", disconnectInfo);
                peer.QuitFromMission = true;
                GameNetwork.AddNetworkPeerToDisconnectAsServer(peer);
                SendToAll($"Игрок {peer.UserName} был исключен сервером ({reason}).");
                return true;
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Admin] Error kicking peer {peer.UserName}: {ex.Message}");
                return false;
            }
        }

        public static bool BanPlayer(NetworkCommunicator peer, NetworkCommunicator banner, string reason)
        {
            if (peer == null || peer.IsServerPeer) return false;
            try
            {
                string id = peer.VirtualPlayer?.Id.ToString() ?? "Unknown";
                string bannerName = banner?.UserName ?? "Console";
                string logLine = $"{id} # Player {peer.UserName}, banned by [{bannerName}] at {DateTime.Now:dd/MM/yyyy HH:mm:ss} reason: {reason}";

                string[] banPaths = new string[]
                {
                    Path.Combine(BasePath.Name, "Modules", "Native", "banlist.txt"),
                    Path.Combine(BasePath.Name, "Modules", "DaewdenM", "banlist.txt"),
                    Path.Combine(BasePath.Name, "Modules", "DWDN_TRY", "banlist.txt")
                };

                foreach (var path in banPaths)
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(path);
                        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        File.AppendAllText(path, logLine + Environment.NewLine);
                    }
                    catch { }
                }

                KickPlayer(peer, "Banned: " + reason);
                SendToAll($"Игрок {peer.UserName} был заблокирован ({reason}).");
                return true;
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Admin] Error banning peer {peer.UserName}: {ex.Message}");
                return false;
            }
        }

        public static void SetMute(NetworkCommunicator peer, bool mute)
        {
            if (peer == null) return;
            peer.IsMuted = mute;
            try
            {
                GameNetwork.BeginBroadcastModuleEvent();
                GameNetwork.WriteMessage(new SyncPlayerMuteState(peer.VirtualPlayer.Id, mute));
                GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.IncludeUnsynchronizedClients);
            }
            catch { }

            SendToAll($"Игрок {peer.UserName} был {(mute ? "заглушен" : "разглушен")}.");
        }

        public static bool EndWarmup()
        {
            var warmup = Mission.Current?.GetMissionBehavior<MultiplayerWarmupComponent>();
            if (warmup == null || !warmup.IsInWarmup) return false;
            warmup.EndWarmupProgress();
            SendToAll("Разминка завершена администратором.");
            return true;
        }

        public static bool ExtendWarmup()
        {
            var warmup = Mission.Current?.GetMissionBehavior<MultiplayerWarmupComponent>();
            if (warmup == null || !warmup.IsInWarmup) return false;
            try
            {
                warmup.GetType().GetProperty("WarmupState", BindingFlags.Public | BindingFlags.Instance)
                    ?.SetValue(warmup, MultiplayerWarmupComponent.WarmupStates.InProgress);
                var timerField = warmup.GetType().GetField("_timerComponent", BindingFlags.NonPublic | BindingFlags.Instance);
                var timer = timerField?.GetValue(warmup) as MultiplayerTimerComponent;
                timer?.StartTimerAsServer(MultiplayerWarmupComponent.TotalWarmupDuration);
                SendToAll("Разминка продлена администратором.");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void EndRoundOrMission()
        {
            SendToAll("Раунд/миссия завершена администратором.");
            Mission.Current?.EndMission();
        }

        public static bool SetNextMap(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName)) return false;
            try
            {
                MultiplayerOptions.OptionType.Map.SetValue(mapName, MultiplayerOptions.MultiplayerOptionsAccessMode.NextMapOptions);
                SendToAll($"Следующая карта установлена: {mapName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN Admin] SetNextMap error: {ex.Message}");
                return false;
            }
        }

        public static void SetSpeed(Agent agent, float multiplier)
        {
            if (agent == null || !agent.IsActive()) return;
            agent.SetMaximumSpeedLimit(multiplier, true);
            if (agent.MountAgent != null && agent.MountAgent.IsActive())
            {
                agent.MountAgent.SetMaximumSpeedLimit(multiplier, true);
            }
        }

        public static void MarkPosition(NetworkCommunicator peer)
        {
            if (peer?.ControlledAgent == null || !peer.ControlledAgent.IsActive()) return;
            _markedPositions[peer] = peer.ControlledAgent.Position;
            SendToPeer(peer, $"Позиция сохранена: {peer.ControlledAgent.Position}");
        }

        public static void Recall(NetworkCommunicator peer, List<NetworkCommunicator> targets)
        {
            if (peer == null || !_markedPositions.TryGetValue(peer, out var savedPos))
            {
                SendToPeer(peer, "Нет сохраненной точки! Сначала используйте !mark");
                return;
            }

            foreach (var target in targets)
            {
                if (target.ControlledAgent != null && target.ControlledAgent.IsActive())
                {
                    target.ControlledAgent.TeleportToPosition(savedPos);
                }
            }
            SendToPeer(peer, $"Телепортировано целей: {targets.Count} на сохраненную точку.");
        }

        public static void TickMission(float dt)
        {
            // Автоматическая синхронизация прав админа для игроков, авторизовавшихся через консоль (adminpassword)
            if (GameNetwork.NetworkPeers != null)
            {
                foreach (var peer in GameNetwork.NetworkPeers)
                {
                    if (peer != null && peer.IsConnectionActive && peer.IsAdmin && !_authenticatedPeers.Contains(peer))
                    {
                        SetPeerAdmin(peer, true);
                        SendToPeer(peer, "Кастомный DWDN Admin Tools активирован (нативный пароль принят). Нажмите [F10] или [Insert] для открытия.");
                        Debug.Print($"[DWDN Admin] Peer {peer.UserName} auto-promoted to custom admin via native peer.IsAdmin.");
                    }
                }
            }

            // Поддержание бессмертия для игроков в GodMode при смене агента / раунда
            foreach (var peer in _godModePeers.ToList())
            {
                if (peer.ControlledAgent != null && peer.ControlledAgent.IsActive())
                {
                    if (peer.ControlledAgent.CurrentMortalityState != Agent.MortalityState.Invulnerable)
                    {
                        peer.ControlledAgent.SetMortalityState(Agent.MortalityState.Invulnerable);
                    }
                    if (peer.ControlledAgent.Health < peer.ControlledAgent.HealthLimit)
                    {
                        peer.ControlledAgent.Health = peer.ControlledAgent.HealthLimit;
                    }

                    if (peer.ControlledAgent.MountAgent != null && peer.ControlledAgent.MountAgent.IsActive())
                    {
                        if (peer.ControlledAgent.MountAgent.CurrentMortalityState != Agent.MortalityState.Invulnerable)
                        {
                            peer.ControlledAgent.MountAgent.SetMortalityState(Agent.MortalityState.Invulnerable);
                        }
                        if (peer.ControlledAgent.MountAgent.Health < peer.ControlledAgent.MountAgent.HealthLimit)
                        {
                            peer.ControlledAgent.MountAgent.Health = peer.ControlledAgent.MountAgent.HealthLimit;
                        }
                    }
                }
            }
        }

        public static bool TryLogin(NetworkCommunicator peer, string password)
        {
            if (peer == null) return false;
            Initialize();

            if (string.IsNullOrWhiteSpace(_adminPassword) || string.IsNullOrWhiteSpace(password)) return false;
            string pass = password.Trim();

            if (string.Equals(pass, _adminPassword, StringComparison.OrdinalIgnoreCase))
            {
                SetPeerAdmin(peer, true);
                SendToPeer(peer, "Успешная авторизация! Добро пожаловать, Администратор.");
                Debug.Print($"[DWDN Admin] Peer {peer.UserName} ({peer.VirtualPlayer?.Id}) logged in as admin via password.");
                return true;
            }

            return false;
        }

        public static bool HandleCommand(NetworkCommunicator executor, string fullMessage)
        {
            Initialize();

            if (string.IsNullOrWhiteSpace(fullMessage) || !fullMessage.StartsWith("!"))
                return false;

            string raw = fullMessage.Substring(1).Trim();
            if (string.IsNullOrEmpty(raw)) return false;

            string[] tokens = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = tokens[0].ToLowerInvariant();
            string args = tokens.Length > 1 ? raw.Substring(cmd.Length).Trim() : string.Empty;

            // 1. Общедоступная авторизация
            if (cmd == "login")
            {
                if (string.IsNullOrEmpty(args))
                {
                    SendToPeer(executor, "Использование: !login <пароль>");
                    return true;
                }

                if (args == _adminPassword || args.Equals(_adminPassword, StringComparison.OrdinalIgnoreCase))
                {
                    SetPeerAdmin(executor, true);
                    SendToPeer(executor, "Успешная авторизация! Добро пожаловать, Администратор.");
                    Debug.Print($"[DWDN Admin] Peer {executor.UserName} ({executor.VirtualPlayer?.Id}) logged in as admin.");
                }
                else
                {
                    SendToPeer(executor, "Неверный пароль администратора!");
                }
                return true;
            }

            // 2. Общедоступная справка и инфо
            if (cmd == "help" || cmd == "?")
            {
                PrintHelp(executor);
                return true;
            }

            if (cmd == "specs")
            {
                ListSpectators(executor);
                return true;
            }

            // Проверка прав администратора для остальных команд
            if (!IsAdmin(executor))
            {
                SendToPeer(executor, "Доступ запрещен. Авторизуйтесь: !login <пароль>");
                return true;
            }

            // Команды Администратора
            switch (cmd)
            {
                case "logout":
                    SetPeerAdmin(executor, false);
                    SendToPeer(executor, "Вы вышли из режима Администратора.");
                    break;

                // --- ТЕЛЕПОРТАЦИЯ ---
                case "goto":
                    {
                        if (string.IsNullOrEmpty(args))
                        {
                            SendToPeer(executor, "Использование: !goto <ник>");
                            break;
                        }
                        var targets = ResolveTargets(executor, args);
                        if (targets.Count == 0 || targets[0].ControlledAgent == null)
                        {
                            SendToPeer(executor, $"Цель '{args}' не найдена или не заспавнена.");
                            break;
                        }
                        executor.ControlledAgent?.TeleportToPosition(targets[0].ControlledAgent.Position);
                        SendToPeer(executor, $"Вы переместились к {targets[0].UserName}.");
                    }
                    break;

                case "bring":
                    {
                        if (string.IsNullOrEmpty(args))
                        {
                            SendToPeer(executor, "Использование: !bring <ник|all|team1|team2>");
                            break;
                        }
                        var targets = ResolveTargets(executor, args);
                        if (targets.Count == 0)
                        {
                            SendToPeer(executor, $"Цели '{args}' не найдены.");
                            break;
                        }
                        TeleportGroupInFrontOf(executor, targets);
                        SendToPeer(executor, $"Перемещено игроков ({targets.Count}) перед вами.");
                    }
                    break;

                case "tp":
                    {
                        HandleTeleportCommand(executor, tokens);
                    }
                    break;

                case "mark":
                    MarkPosition(executor);
                    break;

                case "recall":
                    {
                        var targets = ResolveTargets(executor, string.IsNullOrEmpty(args) ? "me" : args);
                        Recall(executor, targets);
                    }
                    break;

                // --- СНАБЖЕНИЕ И ЗДОРОВЬЕ ---
                case "heal":
                    {
                        string targetArg = tokens.Length > 1 ? tokens[1] : "me";
                        float hpAmount = -1f;
                        if (tokens.Length > 2 && float.TryParse(tokens[2], out float parsedHp))
                        {
                            hpAmount = parsedHp;
                        }
                        var targets = ResolveTargets(executor, targetArg);
                        foreach (var p in targets)
                        {
                            HealTarget(p.ControlledAgent, hpAmount);
                        }
                        SendToPeer(executor, $"Исцелено {targets.Count} игроков (HP, щиты, боезапас).");
                    }
                    break;

                case "ammo":
                    {
                        string targetArg = tokens.Length > 1 ? tokens[1] : "me";
                        var targets = ResolveTargets(executor, targetArg);
                        foreach (var p in targets)
                        {
                            RefillAmmo(p.ControlledAgent);
                        }
                        SendToPeer(executor, $"Пополнен боезапас для {targets.Count} игроков.");
                    }
                    break;

                case "repair":
                    {
                        string targetArg = tokens.Length > 1 ? tokens[1] : "me";
                        var targets = ResolveTargets(executor, targetArg);
                        foreach (var p in targets)
                        {
                            RepairShields(p.ControlledAgent);
                        }
                        SendToPeer(executor, $"Восстановлена прочность щитов для {targets.Count} игроков.");
                    }
                    break;

                case "god":
                    {
                        string targetArg = tokens.Length > 1 ? tokens[1] : "me";
                        var targets = ResolveTargets(executor, targetArg);
                        foreach (var p in targets)
                        {
                            bool active = ToggleGodMode(p);
                            SendToPeer(executor, $"Режим Бога для {p.UserName}: {(active ? "ВКЛ" : "ВЫКЛ")}.");
                        }
                    }
                    break;

                // --- ДИСЦИПЛИНА И МОДЕРАЦИЯ ---
                case "kick":
                    {
                        if (tokens.Length < 2)
                        {
                            SendToPeer(executor, "Использование: !kick <ник> [причина]");
                            break;
                        }
                        var targets = ResolveTargets(executor, tokens[1]);
                        if (targets.Count == 0)
                        {
                            SendToPeer(executor, $"Игрок '{tokens[1]}' не найден.");
                            break;
                        }
                        string reason = tokens.Length > 2 ? raw.Substring(tokens[0].Length + tokens[1].Length + 1).Trim() : "Решение администратора";
                        KickPlayer(targets[0], reason);
                    }
                    break;

                case "ban":
                    {
                        if (tokens.Length < 2)
                        {
                            SendToPeer(executor, "Использование: !ban <ник> [причина]");
                            break;
                        }
                        var targets = ResolveTargets(executor, tokens[1]);
                        if (targets.Count == 0)
                        {
                            SendToPeer(executor, $"Игрок '{tokens[1]}' не найден.");
                            break;
                        }
                        string reason = tokens.Length > 2 ? raw.Substring(tokens[0].Length + tokens[1].Length + 1).Trim() : "Нарушение правил сервера";
                        BanPlayer(targets[0], executor, reason);
                    }
                    break;

                case "mute":
                    {
                        if (tokens.Length < 2)
                        {
                            SendToPeer(executor, "Использование: !mute <ник>");
                            break;
                        }
                        var targets = ResolveTargets(executor, tokens[1]);
                        if (targets.Count == 0)
                        {
                            SendToPeer(executor, $"Игрок '{tokens[1]}' не найден.");
                            break;
                        }
                        SetMute(targets[0], true);
                    }
                    break;

                case "unmute":
                    {
                        if (tokens.Length < 2)
                        {
                            SendToPeer(executor, "Использование: !unmute <ник>");
                            break;
                        }
                        var targets = ResolveTargets(executor, tokens[1]);
                        if (targets.Count == 0)
                        {
                            SendToPeer(executor, $"Игрок '{tokens[1]}' не найден.");
                            break;
                        }
                        SetMute(targets[0], false);
                    }
                    break;

                case "freeze":
                    {
                        string targetArg = tokens.Length > 1 ? tokens[1] : "me";
                        var targets = ResolveTargets(executor, targetArg);
                        foreach (var p in targets)
                        {
                            bool frozen = ToggleFreeze(p);
                            SendToPeer(executor, $"Заморозка для {p.UserName}: {(frozen ? "ВКЛ" : "ВЫКЛ")}.");
                        }
                    }
                    break;

                case "unfreeze":
                    {
                        string targetArg = tokens.Length > 1 ? tokens[1] : "me";
                        var targets = ResolveTargets(executor, targetArg);
                        foreach (var p in targets)
                        {
                            if (_frozenPeers.Contains(p))
                            {
                                ToggleFreeze(p);
                            }
                        }
                        SendToPeer(executor, $"Игроки разморожены.");
                    }
                    break;

                case "slay":
                case "kill":
                    {
                        if (tokens.Length < 2)
                        {
                            SendToPeer(executor, "Использование: !slay <ник|all>");
                            break;
                        }
                        var targets = ResolveTargets(executor, tokens[1]);
                        foreach (var p in targets)
                        {
                            Slay(p.ControlledAgent);
                        }
                        SendToPeer(executor, $"Уничтожено {targets.Count} бойцов.");
                    }
                    break;

                case "disarm":
                    {
                        string targetArg = tokens.Length > 1 ? tokens[1] : "me";
                        var targets = ResolveTargets(executor, targetArg);
                        foreach (var p in targets)
                        {
                            Disarm(p.ControlledAgent);
                        }
                        SendToPeer(executor, $"Оружие выбито у {targets.Count} игроков.");
                    }
                    break;

                case "dismount":
                    {
                        string targetArg = tokens.Length > 1 ? tokens[1] : "me";
                        var targets = ResolveTargets(executor, targetArg);
                        foreach (var p in targets)
                        {
                            Dismount(p.ControlledAgent);
                        }
                        SendToPeer(executor, $"Спешено {targets.Count} всадников.");
                    }
                    break;

                // --- УПРАВЛЕНИЕ СЕРВЕРОМ И ОПОВЕЩЕНИЯ ---
                case "warmup":
                    {
                        string sub = tokens.Length > 1 ? tokens[1].ToLowerInvariant() : "";
                        if (sub == "end" || sub == "stop")
                        {
                            if (!EndWarmup()) SendToPeer(executor, "Сервер сейчас не находится в фазе разминки.");
                        }
                        else if (sub == "extend" || sub == "restart")
                        {
                            if (!ExtendWarmup()) SendToPeer(executor, "Сервер сейчас не находится в фазе разминки.");
                        }
                        else
                        {
                            SendToPeer(executor, "Использование: !warmup end | !warmup extend");
                        }
                    }
                    break;

                case "endround":
                case "endmission":
                    EndRoundOrMission();
                    break;

                case "map":
                    {
                        if (tokens.Length < 2)
                        {
                            SendToPeer(executor, "Использование: !map <название_сцены>");
                            break;
                        }
                        SetNextMap(tokens[1]);
                    }
                    break;

                case "say":
                    {
                        if (string.IsNullOrEmpty(args))
                        {
                            SendToPeer(executor, "Использование: !say <текст>");
                            break;
                        }
                        SendToAll($"[ADMIN] {args}");
                    }
                    break;

                case "broadcast":
                case "alert":
                    {
                        if (string.IsNullOrEmpty(args))
                        {
                            SendToPeer(executor, "Использование: !broadcast <текст>");
                            break;
                        }
                        BroadcastAlert($"[ВНИМАНИЕ] {args}");
                    }
                    break;

                case "speed":
                    {
                        if (tokens.Length < 2)
                        {
                            SendToPeer(executor, "Использование: !speed [цель] <множитель>");
                            break;
                        }
                        string targetArg = "me";
                        float mult = 1.0f;
                        if (tokens.Length == 2)
                        {
                            float.TryParse(tokens[1], out mult);
                        }
                        else
                        {
                            targetArg = tokens[1];
                            float.TryParse(tokens[2], out mult);
                        }
                        var targets = ResolveTargets(executor, targetArg);
                        foreach (var p in targets)
                        {
                            SetSpeed(p.ControlledAgent, mult);
                        }
                        SendToPeer(executor, $"Скорость для {targets.Count} игроков изменена на x{mult:0.0}.");
                    }
                    break;

                case "info":
                    {
                        string targetArg = tokens.Length > 1 ? tokens[1] : "me";
                        var targets = ResolveTargets(executor, targetArg);
                        if (targets.Count == 0)
                        {
                            SendToPeer(executor, $"Игрок '{targetArg}' не найден.");
                            break;
                        }
                        foreach (var p in targets)
                        {
                            var mp = p.GetComponent<MissionPeer>();
                            string teamStr = mp?.Team != null ? mp.Team.Side.ToString() : "Spectator";
                            float hp = p.ControlledAgent != null ? p.ControlledAgent.Health : 0f;
                            SendToPeer(executor, $"[INFO] {p.UserName} | ID: {p.VirtualPlayer?.Id} | Ping: {p.AveragePingInMilliseconds}ms | Команда: {teamStr} | HP: {hp:0}/{p.ControlledAgent?.HealthLimit ?? 0:0}");
                        }
                    }
                    break;

                case "artylimit":
                    {
                        if (tokens.Length < 2 || !int.TryParse(tokens[1], out int limit))
                        {
                            SendToPeer(executor, $"Текущий лимит орудий на команду: {DWDN_FieldArtilleryBehavior.MaxCannonsPerTeam}. Использование: !artylimit <N>");
                            break;
                        }
                        DWDN_FieldArtilleryBehavior.MaxCannonsPerTeam = Math.Max(1, limit);
                        SendToPeer(executor, $"Лимит артиллерии на команду установлен: {DWDN_FieldArtilleryBehavior.MaxCannonsPerTeam} шт.");
                    }
                    break;

                case "cannons":
                    {
                        var cannons = DWDN_FieldArtilleryBehavior.ActiveEngines;
                        if (cannons == null || cannons.Count == 0)
                        {
                            SendToPeer(executor, "[АРТИЛЛЕРИЯ] На поле боя нет развернутых орудий.");
                            break;
                        }
                        SendToPeer(executor, $"=== ОРУДИЯ НА ПОЛЕ ({cannons.Count} шт., Лимит/команда: {DWDN_FieldArtilleryBehavior.MaxCannonsPerTeam}) ===");
                        foreach (var c in cannons)
                        {
                            float hp = c.Destructable != null ? c.Destructable.HitPoint : c.Spec.MaxHp;
                            string status = c.IsDestroyed ? $"РАЗРУШЕНА (кд {Math.Max(0, DWDN_FieldArtilleryBehavior.DestroyCooldownSeconds - c.DestroyTimer):0}с)" : $"ГОТОВО (HP: {hp:0}/{c.Spec.MaxHp:0}, Толкает: {c.PushingAgent?.Name ?? "никто"})";
                            SendToPeer(executor, $"# {c.Id} | {c.Spec.Name} | Сторона: {c.TeamSide} | Владелец: {c.OwnerPlayerId} | {status} | Поз: ({c.Position.x:0.0}, {c.Position.y:0.0}, {c.Position.z:0.0})");
                        }
                    }
                    break;

                case "clearcannons":
                    {
                        DWDN_FieldArtilleryBehavior.CleanupAllCannons();
                        SendToPeer(executor, "Все артиллерийские орудия на поле боя очищены.");
                    }
                    break;

                case "spawncannon":
                    {
                        if (executor.ControlledAgent == null || Mission.Current == null)
                        {
                            SendToPeer(executor, "Для спавна орудия ваш агент должен быть жив на поле боя.");
                            break;
                        }

                        string typeKey = tokens.Length > 1 ? tokens[1].ToLower() : "mangonel";
                        var spec = DWDN_FieldArtilleryBehavior.GetSpecForWeaponItem(typeKey)
                                   ?? DWDN_FieldArtilleryBehavior.GetSpecForWeaponItem(
                                       typeKey.Contains("ballista") ? "ballista" : "mangonel");

                        if (spec == null)
                        {
                            SendToPeer(executor, "Неизвестный тип орудия. Доступные: mangonel, ballista");
                            break;
                        }

                        Vec2 forward = executor.ControlledAgent.LookDirection.AsVec2.Normalized();
                        if (forward.LengthSquared < 0.01f) forward = new Vec2(0f, 1f);
                        Vec3 spawnPos = executor.ControlledAgent.Position + new Vec3(forward.x * 4.0f, forward.y * 4.0f, 0f);

                        BattleSideEnum side = executor.ControlledAgent.Team?.Side ?? BattleSideEnum.Attacker;
                        string playerId = executor.VirtualPlayer?.Id.ToString() ?? "admin";

                        var spawned = DWDN_FieldArtilleryBehavior.SpawnEngine(Mission.Current, spec, side, spawnPos, forward, playerId);
                        if (spawned != null)
                        {
                            SendToPeer(executor, $"[АДМИН] Орудие {spec.Name} успешно заспавнено перед вами (ID: {spawned.Id})!");
                        }
                        else
                        {
                            SendToPeer(executor, "[АДМИН] Ошибка создания сущности орудия.");
                        }
                    }
                    break;

                default:
                    SendToPeer(executor, $"Неизвестная команда '{cmd}'. Напишите !help для списка возможностей.");
                    break;
            }

            return true;
        }

        private static void HandleTeleportCommand(NetworkCommunicator executor, string[] tokens)
        {
            if (tokens.Length < 2)
            {
                SendToPeer(executor, "Использование: !tp <цель> me | !tp <цель1> <цель2> | !tp all me | !tp <цель> <x> <y> [z]");
                return;
            }

            // 1. Проверка на координаты: !tp <цель> <x> <y> [z]
            if (tokens.Length >= 4 && float.TryParse(tokens[2], out float posX) && float.TryParse(tokens[3], out float posY))
            {
                float posZ = 0f;
                if (tokens.Length >= 5) float.TryParse(tokens[4], out posZ);

                var targets = ResolveTargets(executor, tokens[1]);
                Vec3 targetPos = new Vec3(posX, posY, posZ);
                if (posZ == 0f && Mission.Current?.Scene != null)
                {
                    float groundZ = 0f;
                    if (Mission.Current.Scene.GetHeightAtPoint(targetPos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref groundZ))
                    {
                        targetPos.z = groundZ;
                    }
                }

                foreach (var p in targets)
                {
                    p.ControlledAgent?.TeleportToPosition(targetPos);
                }
                SendToPeer(executor, $"Перемещено {targets.Count} игроков на координаты ({posX:0.0}, {posY:0.0}, {targetPos.z:0.0}).");
                return;
            }

            // 2. Обычный телепорт или групповой
            string target1Str = tokens[1];
            string target2Str = tokens.Length > 2 ? tokens[2] : "me";

            var targets1 = ResolveTargets(executor, target1Str);
            var targets2 = ResolveTargets(executor, target2Str);

            if (targets1.Count == 0)
            {
                SendToPeer(executor, $"Цель '{target1Str}' не найдена.");
                return;
            }

            if (targets2.Count == 0 || targets2[0].ControlledAgent == null)
            {
                SendToPeer(executor, $"Точка назначения '{target2Str}' не найдена или не заспавнена.");
                return;
            }

            if (targets1.Count > 1 || target1Str.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                target1Str.Equals("team1", StringComparison.OrdinalIgnoreCase) || target1Str.Equals("team2", StringComparison.OrdinalIgnoreCase))
            {
                TeleportGroupInFrontOf(targets2[0], targets1);
                SendToPeer(executor, $"Группа игроков ({targets1.Count}) построена перед {targets2[0].UserName}.");
            }
            else
            {
                targets1[0].ControlledAgent?.TeleportToPosition(targets2[0].ControlledAgent.Position);
                SendToPeer(executor, $"{targets1[0].UserName} телепортирован к {targets2[0].UserName}.");
            }
        }

        private static void ListSpectators(NetworkCommunicator executor)
        {
            var spectators = new List<string>();
            var specTeam = Mission.Current?.SpectatorTeam;
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.IsSynchronized)
                {
                    var mp = peer.GetComponent<MissionPeer>();
                    if (mp != null && (mp.Team == null || mp.Team == specTeam))
                    {
                        spectators.Add(peer.UserName);
                    }
                }
            }

            if (spectators.Count == 0)
            {
                SendToPeer(executor, "В наблюдателях сейчас никого нет.");
            }
            else
            {
                SendToPeer(executor, $"Наблюдатели ({spectators.Count}): " + string.Join(", ", spectators));
            }
        }

        private static void PrintHelp(NetworkCommunicator executor)
        {
            if (!IsAdmin(executor))
            {
                SendToPeer(executor, "=== DWDN Команды Сервера ===");
                SendToPeer(executor, "!login <пароль> - Авторизация администратора");
                SendToPeer(executor, "!specs - Список наблюдателей");
                SendToPeer(executor, "!help - Справка по командам");
                return;
            }

            SendToPeer(executor, "=== DWDN ПАНЕЛЬ АДМИНИСТРАТОРА ===");
            SendToPeer(executor, "[ТЕЛЕПОРТ] !goto <ник> | !bring <ник|all|team1|team2> | !tp <цель1> <цель2> | !tp <цель> <x> <y> [z]");
            SendToPeer(executor, "[МЕТКИ] !mark (сохранить) | !recall [цель] (вернуть на метку)");
            SendToPeer(executor, "[СНАБЖЕНИЕ] !heal [цель] [хп] | !ammo [цель] | !repair [цель] | !god [цель]");
            SendToPeer(executor, "[МОДЕРАЦИЯ] !kick <ник> [причина] | !ban <ник> [причина] | !mute/!unmute <ник>");
            SendToPeer(executor, "[КОНТРОЛЬ] !freeze/!unfreeze [цель] | !slay [цель] | !disarm [цель] | !dismount [цель]");
            SendToPeer(executor, "[АРТИЛЛЕРИЯ] !artylimit <N> | !cannons | !clearcannons | !spawncannon [тип]");
            SendToPeer(executor, "[СЕРВЕР] !warmup end/extend | !endround | !map <название> | !say <текст> | !broadcast <текст> | !speed [цель] <множитель>");
            SendToPeer(executor, "[ИНФО] !info [ник] | !specs | !logout");
        }
    }
}
