using System;
using TaleWorlds.Core;
using TaleWorlds.DotNet;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;

namespace DWDN_Script.Client.UI
{
    /// <summary>
    /// Автономный 3D-превью персонажа на экране выбора класса (архитектура LOTR / OMEE).
    /// Создает независимый AgentVisuals в сцене миссии и выделенную камеру.
    /// Полностью изолирован от нативных пьедесталов и MultiplayerMissionAgentVisualSpawnComponent.
    /// </summary>
    public sealed class DWDN_CharacterPreview
    {
        private const float CameraDistance = 3.6f;
        private const float CameraHeight = 1.6f;
        private const float CameraTargetHeight = 1.1f;

        private static readonly string[] IdleActionNames = new string[]
        {
            "act_sturgia_mp_huskarl_idle",
            "act_empire_mp_archer_militia_idle",
            "act_idle_unarmed_1",
            "act_walk_idle_unarmed",
            "act_inventory_idle_start"
        };
        private static ActionIndexCache _idleAction = ActionIndexCache.act_none;

        private AgentVisuals _visual;
        private Camera _camera;
        private MatrixFrame _anchor = MatrixFrame.Identity;
        private float _yaw;

        public Camera Camera => _camera;

        public bool IsReady => _visual != null && (NativeObject)(object)_camera != (NativeObject)null;

        private static ActionIndexCache ResolveIdleAction()
        {
            if (_idleAction.Index >= 0)
                return _idleAction;

            foreach (string name in IdleActionNames)
            {
                ActionIndexCache action = ActionIndexCache.Create(name);
                if (action.Index >= 0)
                {
                    _idleAction = action;
                    return _idleAction;
                }
            }
            return ActionIndexCache.act_none;
        }

        public void Show(DWDN_PreviewInputs inputs, MatrixFrame anchor)
        {
            Hide();
            _anchor = anchor;
            _yaw = 0f;
            _visual = CreateVisual(inputs);
            if (_visual != null)
            {
                _camera = Camera.CreateCamera();
                PositionCamera();
            }
        }

        public void UpdateLoadout(DWDN_PreviewInputs inputs)
        {
            if (_visual != null)
            {
                _visual.ResetNextFrame();
            }
            _visual = CreateVisual(inputs);
            ApplyYaw();
        }

        public void Rotate(float deltaYaw)
        {
            _yaw += deltaYaw;
            ApplyYaw();
        }

        public void Tick(float dt)
        {
            if (_visual != null)
            {
                _visual.TickVisuals();
            }
        }

        public void Hide()
        {
            if (_visual != null)
            {
                _visual.Reset();
                _visual = null;
            }
            ReleaseCamera();
        }

        public void HideImmediate()
        {
            if (_visual != null)
            {
                _visual.Reset();
                _visual = null;
            }
            ReleaseCamera();
        }

        private void ReleaseCamera()
        {
            if ((NativeObject)(object)_camera != (NativeObject)null)
            {
                _camera.ReleaseCameraEntity();
                _camera = null;
            }
        }

        private AgentVisuals CreateVisual(DWDN_PreviewInputs inputs)
        {
            if (inputs == null || inputs.SkeletonCharacter == null || Mission.Current == null || Mission.Current.Scene == null)
            {
                return null;
            }

            Monster baseMonster = inputs.Monster ?? TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(inputs.Race);
            MatrixFrame bodyFrame = BuildBodyFrame();
            ActionIndexCache idleAction = ResolveIdleAction();

            string actionSetName = ActionSetCode.GenerateActionSetNameWithSuffix(baseMonster, inputs.IsFemale, "_warrior");
            MBActionSet actionSet = MBActionSet.GetActionSet(actionSetName);
            if (!actionSet.IsValid)
            {
                actionSetName = baseMonster.ActionSetCode;
                actionSet = MBActionSet.GetActionSet(actionSetName);
            }

            // Гарантируем, что для человеческого превью слоты Horse/HorseHarness не содержат мешей
            Equipment previewEquip = inputs.Equipment != null ? new Equipment(inputs.Equipment) : new Equipment();
            previewEquip[EquipmentIndex.Horse] = EquipmentElement.Invalid;
            previewEquip[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;

            AgentVisualsData data = new AgentVisualsData()
                .Equipment(previewEquip)
                .BodyProperties(inputs.Body)
                .Race(inputs.Race)
                .Frame(bodyFrame)
                .Scene(Mission.Current.Scene)
                .Monster(baseMonster)
                .ActionSet(actionSet)
                .ActionCode(idleAction)
                .ClothColor1(inputs.ClothingColor1)
                .ClothColor2(inputs.ClothingColor2)
                .Banner(inputs.Banner)
                .UseMorphAnims(true)
                .PrepareImmediately(false)
                .SkeletonType(inputs.IsFemale ? SkeletonType.Female : SkeletonType.Male);

            AgentVisuals visual = AgentVisuals.Create(data, "dwdn_preview", false, false, false);

            EquipmentIndex primarySlot;
            EquipmentIndex offhandSlot;
            bool isTwoHanded;
            previewEquip.GetInitialWeaponIndicesToEquip(out primarySlot, out offhandSlot, out isTwoHanded, Equipment.InitialWeaponEquipPreference.Any);
            if (isTwoHanded)
            {
                offhandSlot = EquipmentIndex.None;
            }

            visual.GetVisuals().SetWieldedWeaponIndices((int)primarySlot, (int)offhandSlot);
            visual.SetAction(idleAction, 0f, true);
            visual.GetVisuals().SetFrame(ref bodyFrame);
            visual.SetVisible(true);

            return visual;
        }

        private MatrixFrame BuildBodyFrame()
        {
            MatrixFrame result = new MatrixFrame(_anchor.rotation, _anchor.origin);
            result.rotation.RotateAboutUp(_yaw);
            return result;
        }

        private void ApplyYaw()
        {
            if (_visual != null)
            {
                MatrixFrame frame = BuildBodyFrame();
                _visual.GetEntity().SetFrame(ref frame, true);
            }
        }

        private void PositionCamera()
        {
            if ((NativeObject)(object)_camera == (NativeObject)null)
                return;

            Vec3 up = new Vec3(0f, 0f, 1f, -1f);
            Vec3 forward = _anchor.rotation.f;
            forward.z = 0f;
            float len = forward.Length;
            forward = (len > 0.0001f) ? (forward * (1f / len)) : new Vec3(0f, 1f, 0f, -1f);

            Vec3 origin = _anchor.origin;
            Vec3 camPos = origin + forward * CameraDistance + up * CameraHeight;
            Vec3 camTarget = origin + up * CameraTargetHeight;

            _camera.LookAt(camPos, camTarget, up);
            _camera.SetFovVertical((float)Math.PI / 4f, Screen.AspectRatio, 0.1f, 200f);
        }
    }
}
