using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using DWDN_Script.Common.Network;

namespace DWDN_Script.Client.UI.Admin
{
    public class DWDN_AdminPanelVM : ViewModel
    {
        private readonly DWDN_AdminMissionView _owner;
        private string _titleText = "DWDN ADMIN TOOLS";
        private string _subtitleText = "Панель Управления Сервером";
        private string _searchText = "";
        private string _playerCountText = "Игроков: 0";
        private string _commandText = "";
        private bool _hasPlayers;
        private float _refreshTimer;

        private readonly List<DWDN_AdminPlayerRowVM> _allPlayers = new List<DWDN_AdminPlayerRowVM>();

        [DataSourceProperty]
        public string TitleText
        {
            get => _titleText;
            set { _titleText = value; OnPropertyChangedWithValue(value, "TitleText"); }
        }

        [DataSourceProperty]
        public string SubtitleText
        {
            get => _subtitleText;
            set { _subtitleText = value; OnPropertyChangedWithValue(value, "SubtitleText"); }
        }

        [DataSourceProperty]
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value ?? "";
                    OnPropertyChangedWithValue(value, "SearchText");
                    ApplyFilter();
                }
            }
        }

        [DataSourceProperty]
        public string PlayerCountText
        {
            get => _playerCountText;
            set { _playerCountText = value; OnPropertyChangedWithValue(value, "PlayerCountText"); }
        }

        [DataSourceProperty]
        public string CommandText
        {
            get => _commandText;
            set { _commandText = value; OnPropertyChangedWithValue(value, "CommandText"); }
        }

        [DataSourceProperty]
        public bool HasPlayers
        {
            get => _hasPlayers;
            set { _hasPlayers = value; OnPropertyChangedWithValue(value, "HasPlayers"); }
        }

        [DataSourceProperty]
        public MBBindingList<DWDN_AdminPlayerRowVM> PlayerRows { get; } = new MBBindingList<DWDN_AdminPlayerRowVM>();

        public DWDN_AdminPanelVM(DWDN_AdminMissionView owner)
        {
            _owner = owner;
            RefreshPlayers();
        }

        public void Tick(float dt)
        {
            _refreshTimer += dt;
            if (_refreshTimer >= 1.5f)
            {
                _refreshTimer = 0f;
                RefreshPlayers();
            }
        }

        public void RefreshPlayers()
        {
            _allPlayers.Clear();
            if (GameNetwork.NetworkPeers != null)
            {
                foreach (var peer in GameNetwork.NetworkPeers)
                {
                    if (peer == null || peer.IsServerPeer) continue;

                    string id = peer.VirtualPlayer?.Id.ToString() ?? "-";
                    string name = peer.UserName ?? "Unknown";
                    string team = "Зритель";
                    var mp = peer.GetComponent<MissionPeer>();
                    if (mp?.Team != null)
                    {
                        team = mp.Team.Side == BattleSideEnum.Attacker ? "Атакующие" :
                               mp.Team.Side == BattleSideEnum.Defender ? "Защитники" : "Зритель";
                    }

                    string status = (peer.ControlledAgent != null && peer.ControlledAgent.IsActive()) ? "Жив" : "Мертв / Нет";
                    int ping = (int)Math.Round(peer.AveragePingInMilliseconds);

                    _allPlayers.Add(new DWDN_AdminPlayerRowVM(this, id, name, team, status, ping));
                }
            }

            ApplyFilter();
        }

        public void ApplyFilter()
        {
            PlayerRows.Clear();
            string q = _searchText.Trim().ToLowerInvariant();
            foreach (var p in _allPlayers)
            {
                if (string.IsNullOrEmpty(q) ||
                    p.PlayerName.ToLowerInvariant().Contains(q) ||
                    p.PlayerId.ToLowerInvariant().Contains(q) ||
                    p.TeamName.ToLowerInvariant().Contains(q))
                {
                    PlayerRows.Add(p);
                }
            }

            PlayerCountText = $"Игроков онлайн: {PlayerRows.Count} (всего: {_allPlayers.Count})";
            HasPlayers = PlayerRows.Count > 0;
        }

        public void SendAction(string action, string targetId = "", string argument = "")
        {
            try
            {
                GameNetwork.BeginModuleEventAsClient();
                GameNetwork.WriteMessage(new RequestAdminActionMessage(action, targetId, argument));
                GameNetwork.EndModuleEventAsClient();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[DWDN Admin Error] {ex.Message}"));
            }
        }

        // Быстрые глобальные действия
        public void ExecuteBringAll() => SendAction("bring_all");
        public void ExecuteHealAll() => SendAction("heal_all");
        public void ExecuteKillBots() => SendAction("kill_bots");
        public void ExecuteKillAll() => SendAction("kill_all");
        public void ExecuteEndWarmup() => SendAction("end_warmup");
        public void ExecuteExtendWarmup() => SendAction("extend_warmup");
        public void ExecuteEndRound() => SendAction("end_round");
        public void ExecuteRefresh() => RefreshPlayers();
        public void ExecuteClose() => _owner?.ClosePanel();

        public void ExecuteSendCommand()
        {
            if (!string.IsNullOrWhiteSpace(_commandText))
            {
                SendAction(_commandText.Trim());
                CommandText = "";
            }
        }
    }

    public class DWDN_AdminPlayerRowVM : ViewModel
    {
        private readonly DWDN_AdminPanelVM _parent;

        [DataSourceProperty]
        public string PlayerId { get; }

        [DataSourceProperty]
        public string PlayerName { get; }

        [DataSourceProperty]
        public string TeamName { get; }

        [DataSourceProperty]
        public string StatusText { get; }

        [DataSourceProperty]
        public string PingText { get; }

        public DWDN_AdminPlayerRowVM(DWDN_AdminPanelVM parent, string id, string name, string team, string status, int ping)
        {
            _parent = parent;
            PlayerId = id;
            PlayerName = name;
            TeamName = team;
            StatusText = status;
            PingText = ping > 0 ? $"{ping} ms" : "-";
        }

        public void ExecuteHeal() => _parent.SendAction("heal", PlayerId);
        public void ExecuteGodMode() => _parent.SendAction("god", PlayerId);
        public void ExecuteTeleport() => _parent.SendAction("goto", PlayerId);
        public void ExecuteBring() => _parent.SendAction("bring", PlayerId);
        public void ExecuteFreeze() => _parent.SendAction("freeze", PlayerId);
        public void ExecuteDisarm() => _parent.SendAction("disarm", PlayerId);
        public void ExecuteSlay() => _parent.SendAction("slay", PlayerId);
        public void ExecuteKick() => _parent.SendAction("kick", PlayerId);
        public void ExecuteBan() => _parent.SendAction("ban", PlayerId);
    }
}
