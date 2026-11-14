using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using DWDN_Script.Client.UI.Admin;

namespace DWDN_Script
{
    class DWDN_InputBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (Mission == null || Mission.CurrentState != Mission.State.Continuing)
                return;

            if (Input.IsKeyPressed(InputKey.N))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Вы нажали клавишу N!",
                    Colors.Green
                ));
                MBInformationManager.AddQuickInformation(new TextObject("{=!}Команда выполнена: активация навыка!"), 0, null);
            }

            if (Input.IsKeyDown(InputKey.LeftShift) && Input.IsKeyPressed(InputKey.F))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[DWDN] Нажата комбинация Shift + F",
                    Colors.Cyan
                ));
            }

            // Горячие клавиши Admin Tools: F10 или Insert
            if (Input.IsKeyPressed(InputKey.F10) || Input.IsKeyPressed(InputKey.Insert))
            {
                var adminView = DWDN_AdminMissionView.Instance;
                if (adminView != null)
                {
                    if (adminView.CanOpenAdminPanel())
                    {
                        adminView.TogglePanel();
                    }
                    else
                    {
                        adminView.PromptForAdminPassword();
                    }
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "=== DWDN ADMIN TOOLS [F10/Insert] ===",
                        Colors.Cyan
                    ));
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Авторизация в чате: !login 25 | Помощь: !help",
                        Colors.White
                    ));
                }
            }
        }
    }
}
