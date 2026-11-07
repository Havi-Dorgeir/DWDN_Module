using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer.GauntletUI.Mission;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using DWDN_Script.Client.UI;

namespace DWDN_Script.Client.Behaviors
{
    /// <summary>
    /// Клиентский сетевой компонент DWDN (архитектура LOTR / OMEE).
    /// Гарантирует чистое отключение нативного MissionGauntletClassLoadout
    /// и внедрение DWDN_SelectionMissionView в MissionScreen через AddMissionView.
    /// </summary>
    public class DWDN_ClientMissionComponent : MissionNetwork
    {
        private bool _installed;

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            InstallIfNeeded();
        }

        private void InstallIfNeeded()
        {
            if (_installed || Mission == null) return;

            ScreenBase topScreen = ScreenManager.TopScreen;
            MissionScreen screen = topScreen as MissionScreen ?? MissionState.Current?.Handler as MissionScreen;

            if (screen != null)
            {
                MissionGauntletClassLoadout vanillaLoadout = Mission.GetMissionBehavior<MissionGauntletClassLoadout>();
                if (vanillaLoadout != null)
                {
                    try
                    {
                        ((MissionView)vanillaLoadout).OnMissionScreenFinalize();
                        screen.UnregisterView((MissionView)vanillaLoadout);
                        Debug.Print("[DWDN] Ванильный MissionGauntletClassLoadout успешно подавлен (UnregisterView).");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.Print($"[DWDN ERROR] Ошибка при подавлении ванильного loadout: {ex}");
                    }
                }

                if (Mission.GetMissionBehavior<DWDN_SelectionMissionView>() == null)
                {
                    try
                    {
                        screen.AddMissionView(new DWDN_SelectionMissionView());
                        Debug.Print("[DWDN] DWDN_SelectionMissionView успешно установлен через AddMissionView.");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.Print($"[DWDN ERROR] Ошибка при установке DWDN_SelectionMissionView: {ex}");
                    }
                }

                _installed = true;
            }
        }

        public override void OnRemoveBehavior()
        {
            _installed = false;
            base.OnRemoveBehavior();
        }

        protected override void OnEndMission()
        {
            _installed = false;
            base.OnEndMission();
        }
    }
}
