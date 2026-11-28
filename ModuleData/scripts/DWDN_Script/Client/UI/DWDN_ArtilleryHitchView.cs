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

            // Находим ближайшее орудие по его дышлу (HitchPosition) или по тягловому коню всадника
            DWDN_FieldArtilleryBehavior.EngineInstance closestEngine = null;
            float closestDist = 4.0f;

            try
            {
                var activeEngines = DWDN_FieldArtilleryBehavior.ActiveEngines;
                if (activeEngines != null)
                {
                    for (int i = 0; i < activeEngines.Count; i++)
                    {
                        var eng = activeEngines[i];
                        if (eng == null || eng.IsDestroyed) continue;

                        // Если игрок сидит верхом на тягловом коне этого орудия — это точно целевое орудие возницы
                        if (mainAgent.MountAgent != null && eng.HorseAgent == mainAgent.MountAgent)
                        {
                            closestEngine = eng;
                            closestDist = 0f;
                            break;
                        }

                        float d = mainAgent.Position.Distance(eng.HitchPosition);
                        if (d < closestDist)
                        {
                            closestDist = d;
                            closestEngine = eng;
                        }
                    }
                }
            }
            catch
            {
                closestEngine = null;
            }

            if (closestEngine == null)
            {
                ResetInteraction();
                return;
            }

            // Защита: если боец активно управляет осадным орудием (наводчик/заряжающий) — полностью отключаем HUD сцепки
            if (mainAgent.CurrentlyUsedGameObject != null || closestEngine.SiegeWeapon?.PilotAgent == mainAgent)
            {
                ResetInteraction();
                return;
            }

            Vec3 hitchPos = closestEngine.HitchPosition;
            var cam = MissionScreen?.CombatCamera;
            Vec3 camPos = cam != null ? cam.Position : mainAgent.GetEyeGlobalPosition();
            Vec3 toHitch = (hitchPos - camPos).NormalizedCopy();

            // Проверяем, является ли игрок верховым возницей упряжи данного орудия
            bool isDriver = (mainAgent.MountAgent != null && closestEngine.HorseAgent == mainAgent.MountAgent);

            if (!isDriver)
            {
                // Геометрическое разделение сторон (только для пеших бойцов и сторонних наблюдателей):
                // Дышло (HitchPosition) находится спереди орудия (+Frame.rotation.f).
                // Позиция наводчика и экипажа находится сзади орудия (-Frame.rotation.f).
                // Надпись и маркер должны отображаться ТОЛЬКО со стороны сцепки (спереди от орудия).
                Vec2 engineFwd = closestEngine.Frame.rotation.f.AsVec2.Normalized();
                if (engineFwd.LengthSquared < 0.01f) engineFwd = new Vec2(0f, 1f);

                Vec2 toPlayer = (mainAgent.Position.AsVec2 - hitchPos.AsVec2);
                float forwardOffset = Vec2.DotProduct(toPlayer, engineFwd);

                // Если боец находится позади дышла (внутри лафета или на позиции наводчика) — скрываем сцепку
                if (forwardOffset < -0.35f)
                {
                    ResetInteraction();
                    return;
                }

                // Вектор взгляда на дышло не должен быть направлен сзади наперед (со стороны наводчика в сторону выстрела)
                if (Vec2.DotProduct(toHitch.AsVec2.Normalized(), engineFwd) > 0.35f)
                {
                    ResetInteraction();
                    return;
                }

                // Проверка дистанции (до 2.5м пешком, до 3.5м верхом) и направления прицела
                float maxInteractDist = (mainAgent.MountAgent != null) ? 3.5f : 2.5f;
                if (closestDist > maxInteractDist)
                {
                    ResetPromptOnly();
                    return;
                }

                Vec3 camDir = cam != null ? cam.Direction : mainAgent.LookDirection;
                float dot = Vec3.DotProduct(camDir, toHitch);

                if (dot < 0.88f) // ~28 градусов комфортный угол прицела
                {
                    ResetPromptOnly();
                    return;
                }
            }

            // 1. 3D World-to-Screen проекция маркера дышла на экран (для пеших)
            if (!isDriver && cam != null)
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

            // 2. Проверка прав: создатель орудия, возница, класс артиллериста или союзник по команде
            bool isOwner = !string.IsNullOrEmpty(closestEngine.OwnerPlayerId) &&
                           (closestEngine.OwnerPlayerId == (GameNetwork.MyPeer?.VirtualPlayer?.Id.ToString() ?? "") ||
                            closestEngine.OwnerPlayerId == GameNetwork.MyPeer?.UserName);
            bool isArtillerist = mainAgent.Character?.StringId?.IndexOf("artillerist", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isFriendly = !GameNetwork.IsMultiplayer || mainAgent.Team == null || mainAgent.Team.Side == closestEngine.TeamSide;

            if (!isOwner && !isArtillerist && !isDriver && !isFriendly)
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
                if (mainAgent.MountAgent != null && mainAgent.MountAgent.IsActive() && !DWDN_FieldArtilleryBehavior.IsHorseHitched(mainAgent.MountAgent))
                {
                    if (mainAgent.MountAgent.Position.Distance(hitchPos) <= 4.0f)
                    {
                        targetHorse = mainAgent.MountAgent;
                    }
                }

                if (targetHorse == null && Mission.Agents != null)
                {
                    float closestHorseDistSq = 3.5f * 3.5f;
                    try
                    {
                        var agents = Mission.Agents;
                        for (int i = 0; i < agents.Count; i++)
                        {
                            var a = agents[i];
                            // Разрешены любые активные кони (свои, нейтральные, вражеские), не занятые чужим всадником и не впряженные в другое орудие
                            if (a != null && a.IsMount && a.IsActive() && (a.RiderAgent == null || a.RiderAgent == mainAgent) && !DWDN_FieldArtilleryBehavior.IsHorseHitched(a))
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
                    catch { }
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
