using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

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
                MBInformationManager.AddQuickInformation(new TextObject("{=!}Команда выполнена: активация навыка!"),0,null);
            }
            if (Input.IsKeyDown(InputKey.LeftShift) && Input.IsKeyPressed(InputKey.F))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[DWDN] Нажата комбинация Shift + F",
                    Colors.Cyan
                ));
            }

        }
    }
}
