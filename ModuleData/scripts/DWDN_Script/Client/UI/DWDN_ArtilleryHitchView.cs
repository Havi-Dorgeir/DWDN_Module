using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using DWDN_Script.Common.Network;

namespace DWDN_Script.Client.UI
{
    /// <summary>
    /// Клиентский MissionView для отзывчивого взаимодействия со сцепкой полевой артиллерии DWDN.
    /// Вычисляет виртуальное положение дышла в 3D-мире, проецирует маяк на экран,
    /// отслеживает фокус прицела и контролирует 2-секундное удержание [F] с прерыванием при получении урона.
    /// </summary>
    public class DWDN_ArtilleryHitchView : MissionView
    {
        private GauntletLayer _layer;
        private DWDN_ArtilleryHitchVM _viewModel;
        private float _holdTimer = 0f;
        private float _lastHealth = -1f;
        private const float RequiredHoldSeconds = 2.0f;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            _viewModel = new DWDN_ArtilleryHitchVM();
            _layer = new GauntletLayer("DWDN_ArtilleryHitchHUD", 16, false);
            _layer.LoadMovie("DWDN_ArtilleryHitchHUD", _viewModel);
            MissionScreen?.AddLayer(_layer);
        }

        public override void OnMissionScreenFinalize()
        {
            if (_layer != null && MissionScreen != null)
            {
                MissionScreen.RemoveLayer(_layer);
                _layer = null;
            }
            _viewModel = null;
            base.OnMissionScreenFinalize();
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (_viewModel == null || Mission == null) return;

            Agent mainAgent = Agent.Main;
            if (mainAgent == null || !mainAgent.IsActive())
            {
                ResetInteraction();
                return;
            }

            // Защита от получения урона: любой урон мгновенно сбивает прогресс удержания в ноль!
            if (_lastHealth > 0f && mainAgent.Health < _lastHealth - 0.01f)
            {
                _holdTimer = 0f;
                _viewModel.Progress = 0f;
            }
            _lastHealth = mainAgent.Health;

            var activeEngines = DWDN_FieldArtilleryBehavior.ActiveEngines;
            if (activeEngines == null || activeEngines.Count == 0)
            {
                ResetInteraction();
                return;
            }

            // Находим ближайшее орудие по его дышлу (HitchPosition)
            DWDN_FieldArtilleryBehavior.EngineInstance closestEngine = null;
            float closestDist = 3.6f;

            for (int i = 0; i < activeEngines.Count; i++)
            {
                var eng = activeEngines[i];
                if (eng.IsDestroyed) continue;

                float d = mainAgent.Position.Distance(eng.HitchPosition);
                if (d < closestDist)
                {
                    closestDist = d;
                    closestEngine = eng;
                }
            }

            if (closestEngine == null)
            {
                ResetInteraction();
                return;
            }

            Vec3 hitchPos = closestEngine.HitchPosition;

            // 1. 3D World-to-Screen проекция маркера дышла на экран
            var cam = MissionScreen?.CombatCamera;
            if (cam != null)
            {
                Vec3 vp = cam.WorldPointToViewPortPoint(ref hitchPos);
                if (!float.IsNaN(vp.x) && !float.IsNaN(vp.y) && vp.z > 0.2f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
                {
                    float scale = (_layer != null && _layer.Scale > 0.01f) ? _layer.Scale : 1.0f;
                    float screenW = Screen.RealScreenResolutionWidth / scale;
                    float screenH = Screen.RealScreenResolutionHeight / scale;
                    _viewModel.MarkerX = (vp.x * screenW) - 10f;
                    _viewModel.MarkerY = ((1.0f - vp.y) * screenH) - 10f;
                    _viewModel.IsMarkerVisible = true;
                }
                else
                {
                    _viewModel.IsMarkerVisible = false;
                }
            }
            else
            {
                _viewModel.IsMarkerVisible = false;
            }

            // 2. Проверка дистанции (до 2.5м) и направления взгляда
            if (closestDist > 2.5f)
            {
                ResetPromptOnly();
                return;
            }

            Vec3 camPos = cam != null ? cam.Position : mainAgent.GetEyeGlobalPosition();
            Vec3 camDir = cam != null ? cam.Direction : mainAgent.LookDirection;
            Vec3 toHitch = (hitchPos - camPos).NormalizedCopy();
            float dot = Vec3.DotProduct(camDir, toHitch);

            if (dot < 0.88f) // ~28 градусов комфортный угол прицела
            {
                ResetPromptOnly();
                return;
            }

            // 3. Проверка прав: создатель орудия или класс артиллериста
            bool isOwner = !string.IsNullOrEmpty(closestEngine.OwnerPlayerId) &&
                           (closestEngine.OwnerPlayerId == (GameNetwork.MyPeer?.VirtualPlayer?.Id.ToString() ?? "") ||
                            closestEngine.OwnerPlayerId == GameNetwork.MyPeer?.UserName);
            bool isArtillerist = mainAgent.Character?.StringId?.IndexOf("artillerist", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isOwner && !isArtillerist)
            {
                _viewModel.PromptText = "Управлять сцепкой может только артиллерийский расчет";
                _viewModel.CanInteract = false;
                _viewModel.IsPromptVisible = true;
                _holdTimer = 0f;
                _viewModel.Progress = 0f;
                return;
            }

            // 4. Определение контекста: отцепка vs впряжка
            Agent targetHorse = null;
            bool canInteract = false;

            if (closestEngine.IsHitched)
            {
                _viewModel.PromptText = "[F] Удерживайте, чтобы отцепить орудие (Боевой режим)";
                canInteract = true;
            }
            else
            {
                // Поиск подходящего коня (включая трофейных коней врага!)
                if (mainAgent.MountAgent != null && mainAgent.MountAgent.IsActive())
                {
                    if (mainAgent.MountAgent.Position.Distance(hitchPos) <= 4.0f)
                    {
                        targetHorse = mainAgent.MountAgent;
                    }
                }

                if (targetHorse == null)
                {
                    float closestHorseDistSq = 3.5f * 3.5f;
                    foreach (var a in Mission.Agents)
                    {
                        // Разрешены любые активные кони (свои, нейтральные, вражеские), не занятые чужим всадником
                        if (a.IsMount && a.IsActive() && (a.RiderAgent == null || a.RiderAgent == mainAgent))
                        {
                            float dSq = a.Position.DistanceSquared(hitchPos);
                            if (dSq <= closestHorseDistSq)
                            {
                                closestHorseDistSq = dSq;
                                targetHorse = a;
                            }
                        }
                    }
                }

                if (targetHorse != null)
                {
                    _viewModel.PromptText = "[F] Удерживайте, чтобы впрячь коня (Походный режим)";
                    canInteract = true;
                }
                else
                {
                    _viewModel.PromptText = "⚠ Подведите коня к дышлу для сцепки";
                    canInteract = false;
                }
            }

            _viewModel.CanInteract = canInteract;
            _viewModel.IsPromptVisible = true;

            // 5. Обработка удержания клавиши [F]
            if (canInteract && Input.IsKeyDown(InputKey.F))
            {
                _holdTimer += dt;
                _viewModel.Progress = Math.Min(1.0f, _holdTimer / RequiredHoldSeconds);

                if (_holdTimer >= RequiredHoldSeconds)
                {
                    // Удержание успешно завершено: отдаем сетевой приказ!
                    _holdTimer = 0f;
                    _viewModel.Progress = 0f;

                    bool attach = !closestEngine.IsHitched;
                    int horseIdx = targetHorse?.Index ?? -1;

                    if (GameNetwork.IsClient)
                    {
                        GameNetwork.BeginModuleEventAsClient();
                        GameNetwork.WriteMessage(new RequestToggleArtilleryHitchMessage(closestEngine.Id, horseIdx, attach));
                        GameNetwork.EndModuleEventAsClient();
                    }
                    else
                    {
                        if (attach && targetHorse != null)
                        {
                            DWDN_FieldArtilleryBehavior.HitchEngineToHorse(closestEngine, targetHorse);
                        }
                        else
                        {
                            DWDN_FieldArtilleryBehavior.DeployEngineToCombat(closestEngine);
                        }
                    }
                }
            }
            else
            {
                _holdTimer = 0f;
                _viewModel.Progress = 0f;
            }
        }

        private void ResetInteraction()
        {
            _holdTimer = 0f;
            if (_viewModel != null)
            {
                _viewModel.IsPromptVisible = false;
                _viewModel.IsMarkerVisible = false;
                _viewModel.Progress = 0f;
            }
        }

        private void ResetPromptOnly()
        {
            _holdTimer = 0f;
            if (_viewModel != null)
            {
                _viewModel.IsPromptVisible = false;
                _viewModel.Progress = 0f;
            }
        }
    }
}
