using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DWDN_Script
{
    public class DWDN_FirearmsBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        /// <summary>
        /// Feature toggle for enhanced client-side white black-powder smoke clouds.
        /// Set to false to compare with the legacy dust-burst effect.
        /// </summary>
        public static bool EnableEnhancedWhiteSmoke = true;

        /// <summary>
        /// Feature toggle for the 3-layer fiery muzzle flash (procedural 3D flame mesh + vanilla fire burst + point light).
        /// </summary>
        public static bool EnableEnhancedMuzzleFlash = true;

        // Ванильные эффекты из Native/AssetPackages/particles.tpac (гарантированно доступны в релизном клиенте):
        // - psys_game_blacksmith_flame: 100% чистое полноразмерное кузнечное пламя (prt_shd_fire_1, local=true, vx=0, vy=0, vz=+0.35, 0 дыма!)
        // - invoker: сверхплотный симметричный факел пламени и огненных искр (prt_shd_fire_1, vx=0, vy=0, rate=150, life=0.40, qs=1.0)
        // - psys_game_metal_metal_coll: мгновенные горячие огненные искры (prt_shd_fire_1, alpha(0)=1.0, diffuse=10000, 0 дыма)
        // - psys_smoke: сферический дым (prt_shd_smoke_4, emit_at_once=true, bias=2.0, размер ~1.65м) — основа объема
        // - prt_steam_bath: чисто-белый пар с diffuse_multiplier=3.0 и низким wind_effect=0.30 — высветляет облако до белого
        // - psys_game_sparkle_a: мелкие пороховые искры
        private const string BlacksmithFlameParticle = "psys_game_blacksmith_flame";
        private const string DenseFlameJetParticle = "invoker";
        private const string FierySparksParticle = "psys_game_metal_metal_coll";
        private const string WhiteSmokeBurstParticle = "psys_smoke";
        private const string BrightWhiteSteamParticle = "prt_steam_bath";
        private const string MuzzleSparksParticle = "psys_game_sparkle_a";

        // position из OnAgentShootMissile уже соответствует AmmoOffset ручницы, поэтому вынос вперед минимальный
        private const float MuzzleForwardOffset = 0.08f;
        private const float FirstPersonExtraForwardOffset = 0.14f;

        private const int MaxActiveClouds = 24;
        private const int MaxActiveFlashLights = 12;
        private const float FullDetailDistanceSq = 85f * 85f;
        private const float MaxVisibleDistanceSq = 170f * 170f;

        private sealed class SmokeCloudInstance
        {
            public GameEntity CloudEntity;
            public GameEntity FlashMeshEntity;
            public Mesh FlashMesh;
            public MatrixFrame FlashMeshBaseFrame;
            public readonly List<ParticleSystem> SlowSmokeBursts = new List<ParticleSystem>(28);
            public readonly List<ParticleSystem> BrightWhiteSteams = new List<ParticleSystem>(12);
            public readonly List<ParticleSystem> MuzzleFlames = new List<ParticleSystem>(2);
            public Light FlashLight;
            public float FlashLightPeakIntensity;

            public float Age;
            public float FlashMeshDuration;
            public float FlameBrakeTime;
            public float FlameEmitDuration;
            public float FlameCutoffDuration;
            public float SteamEmitDuration;
            public float FlashLightDuration;
            public float HangBrakeTime;
            public float MaxLifetime;
            public bool FlashMeshHidden;
            public bool FlameBraked;
            public bool FlameStopped;
            public bool FlameHidden;
            public bool SteamStopped;
            public bool FlashLightOff;
            public bool HangBrakeApplied;
        }

        private readonly List<SmokeCloudInstance> _activeClouds = new List<SmokeCloudInstance>(MaxActiveClouds);
        private readonly Random _rng = new Random();
        private Material _cachedFlashMaterial;

        // Sound event IDs (cached)
        private int _soundCrack = -1;
        private int _soundPop = -1;
        private bool _soundsInitialized = false;

        private void InitializeSounds()
        {
            if (_soundsInitialized) return;

            // Light, sharp gunpowder crack and pop (handgonne sound)
            _soundCrack = SoundEvent.GetEventIdFromString("event:/mission/combat/impact/ballista_missile/stone");
            _soundPop = SoundEvent.GetEventIdFromString("event:/mission/combat/impact/missile/pot_breakable");
            _soundsInitialized = true;
        }

        private bool IsFirearm(ItemObject item)
        {
            if (item == null) return false;
            string id = item.StringId;
            if (string.IsNullOrEmpty(id)) return false;

            return id.Equals("hand_cannon", StringComparison.OrdinalIgnoreCase) ||
                   id.Equals("mp_gun_prototype", StringComparison.OrdinalIgnoreCase) ||
                   id.IndexOf("cannon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   id.IndexOf("bombard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   id.IndexOf("handgonne", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   id.IndexOf("firearm", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public override void OnAgentShootMissile(
            Agent shooterAgent,
            EquipmentIndex weaponIndex,
            Vec3 position,
            Vec3 velocity,
            Mat3 orientation,
            bool hasRigidBody,
            int forcedMissileIndex)
        {
            base.OnAgentShootMissile(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);

            if (shooterAgent == null || Mission == null)
                return;

            if (weaponIndex < EquipmentIndex.WeaponItemBeginSlot || weaponIndex >= EquipmentIndex.NumAllWeaponSlots)
                return;

            MissionWeapon weapon = shooterAgent.Equipment[weaponIndex];
            if (weapon.IsEmpty || weapon.Item == null || !IsFirearm(weapon.Item))
                return;

            // Направление ствола берем из LookDirection (синхронизируется по сети и не искажается разбросом accuracy="18")
            Vec3 forward = shooterAgent.LookDirection;
            if (forward.LengthSquared > 0.001f)
            {
                forward.Normalize();
            }
            else if (orientation.f.LengthSquared > 0.001f)
            {
                forward = orientation.f.NormalizedCopy();
            }
            else
            {
                forward = velocity.NormalizedCopy();
            }

            // position уже соответствует точке вылета снаряда (AmmoOffset), поэтому добавляем лишь небольшой вынос к срезу ствола
            float muzzleOffset = MuzzleForwardOffset + (shooterAgent.IsMainAgent ? FirstPersonExtraForwardOffset : 0f);
            Vec3 muzzlePos = position + forward * muzzleOffset;
            Mat3 flashBasis = BuildForwardAsUpBasis(forward);
            MatrixFrame muzzleFrame = new MatrixFrame(flashBasis, muzzlePos);

            if (!GameNetwork.IsDedicatedServer)
            {
                InitializeSounds();

                // ==========================================
                // 1. "ЛЕГКИЙ БАХ" — Clean, crisp handgonne gunshot
                // ==========================================
                if (_soundCrack != -1)
                {
                    Mission.MakeSound(_soundCrack, muzzlePos, false, true, shooterAgent.Index, -1);
                }
                if (_soundPop != -1)
                {
                    Mission.MakeSound(_soundPop, muzzlePos, false, true, shooterAgent.Index, -1);
                }

                // ==========================================
                // 2. ОГНЕННАЯ ДУЛЬНАЯ ВСПЫШКА И ОБЛАКО БЕЛОГО ПОРОХОВОГО ДЫМА
                // ==========================================
                if (EnableEnhancedWhiteSmoke)
                {
                    SpawnEnhancedWhiteSmoke(muzzlePos, position, forward, flashBasis);
                }
                else
                {
                    SpawnLegacySmoke(muzzleFrame, muzzlePos, position, forward, orientation);
                }
            }

            // NOTE: Мы не спавним сторонние AddCustomMissile на сервере, так как нативный выстрел
            // движка Bannerlord уже идеально выпускает свинцовую пулю (165 урона, пробитие щитов).
            // Это гарантирует 100% стабильность выделенного сервера при любых залпах.
        }

        private void SpawnLegacySmoke(MatrixFrame muzzleFrame, Vec3 muzzlePos, Vec3 shotPos, Vec3 forward, Mat3 orientation)
        {
            Mission.AddParticleSystemBurstByName("psys_game_stone_dust_a", muzzleFrame, false);
            Mission.AddParticleSystemBurstByName("psys_siege_door_falling_dust", muzzleFrame, false);

            MatrixFrame smokePlume1 = new MatrixFrame(orientation, muzzlePos + forward * 0.60f);
            Mission.AddParticleSystemBurstByName("psys_game_stone_dust_a", smokePlume1, false);

            MatrixFrame smokePlume2 = new MatrixFrame(orientation, muzzlePos + forward * 1.20f);
            Mission.AddParticleSystemBurstByName("psys_game_stone_dust_a", smokePlume2, false);

            Mission.AddParticleSystemBurstByName(MuzzleSparksParticle, muzzleFrame, false);

            MatrixFrame touchHoleFrame = new MatrixFrame(orientation, shotPos - forward * 0.10f + new Vec3(0f, 0f, 0.04f));
            Mission.AddParticleSystemBurstByName("psys_game_stone_dust_a", touchHoleFrame, false);
        }

        private Vec3 RandomJitter(float radius)
        {
            float rx = ((float)_rng.NextDouble() * 2f - 1f) * radius;
            float ry = ((float)_rng.NextDouble() * 2f - 1f) * radius;
            float rz = ((float)_rng.NextDouble() * 2f - 1f) * radius;
            return new Vec3(rx, ry, rz);
        }

        private static Mat3 BuildForwardAsUpBasis(Vec3 forward)
        {
            Vec3 u = forward.LengthSquared > 0.001f ? forward.NormalizedCopy() : Vec3.Forward;
            Vec3 refAxis = Math.Abs(u.z) < 0.95f ? Vec3.Up : Vec3.Forward;
            Vec3 s = Vec3.CrossProduct(u, refAxis).NormalizedCopy();
            Vec3 f = Vec3.CrossProduct(u, s).NormalizedCopy();
            return new Mat3(s, f, u);
        }

        private int CountActiveFlashLights()
        {
            int count = 0;
            for (int i = 0; i < _activeClouds.Count; i++)
            {
                if (!_activeClouds[i].FlashLightOff && _activeClouds[i].FlashLight != null)
                {
                    count++;
                }
            }
            return count;
        }

        private Material GetOrCreateFlashMaterial()
        {
            if (_cachedFlashMaterial != null && _cachedFlashMaterial.IsValid)
                return _cachedFlashMaterial;

            try
            {
                Material baseMat = Material.GetFromResource("vertex_color_lighting_off");
                if (baseMat == null || !baseMat.IsValid)
                    baseMat = Material.GetFromResource("plain_white_noshadow");
                if (baseMat == null || !baseMat.IsValid)
                    baseMat = Material.GetDefaultMaterial();
                if (baseMat == null || !baseMat.IsValid)
                    return null;

                Material mat = baseMat.CreateCopy();
                if (mat == null || !mat.IsValid)
                    return null;

                mat.SetAlphaBlendMode(Material.MBAlphaBlendMode.Add);
                mat.Flags = mat.Flags
                    | MaterialFlags.TwoSided
                    | MaterialFlags.DontCastShadow
                    | MaterialFlags.NoModifyDepthBuffer
                    | MaterialFlags.RequiresForwardRendering;

                _cachedFlashMaterial = mat;
                return _cachedFlashMaterial;
            }
            catch
            {
                return null;
            }
        }

        private Mesh CreateMuzzleFlashMesh()
        {
            Material mat = GetOrCreateFlashMaterial();
            if (mat == null)
                return null;

            try
            {
                Mesh mesh = Mesh.CreateMeshWithMaterial(mat);
                if (mesh == null)
                    return null;

                // ARGB цвета для аддитивного свечения: бело-золотое ядро -> ярко-оранжевое тело -> прозрачно-красные кончики
                const uint coreColor = 0xFFFFF4C0u;
                const uint midColor = 0xFFFF8818u;
                const uint tipColor = 0x00C82000u;

                Vec2 uv0 = new Vec2(0.5f, 0.5f);
                UIntPtr lockHandle = mesh.LockEditDataWrite();
                try
                {
                    // 1. 8 радиальных языков пламени вдоль +Z (ось ствола) длиной 2.0..2.7 м (увеличено ~2x)
                    const int jetCount = 8;
                    for (int i = 0; i < jetCount; i++)
                    {
                        float angle = (float)(i * Math.PI / jetCount);
                        float cos = (float)Math.Cos(angle);
                        float sin = (float)Math.Sin(angle);
                        float len = 2.00f + (float)_rng.NextDouble() * 0.70f;
                        float halfW = 0.42f + (float)_rng.NextDouble() * 0.14f;
                        float midZ = len * 0.36f;

                        Vec3 pBase = new Vec3(0f, 0f, 0.02f);
                        Vec3 pSideL = new Vec3(cos * halfW, sin * halfW, midZ);
                        Vec3 pSideR = new Vec3(-cos * halfW, -sin * halfW, midZ);
                        Vec3 pTip = new Vec3(0f, 0f, len);

                        // Передняя и задняя половины ромба-языка пламени (плавный градиент от ядра к острию)
                        mesh.AddTriangleWithVertexColors(pBase, pSideL, pSideR, uv0, uv0, uv0, coreColor, midColor, midColor, lockHandle);
                        mesh.AddTriangleWithVertexColors(pSideL, pTip, pSideR, uv0, uv0, uv0, midColor, tipColor, midColor, lockHandle);
                    }

                    // 2. Фронтальная 6-лучевая «звезда» у дульного среза (отлично читается спереди и под острым углом)
                    const int starRays = 6;
                    Vec3 starCenter = new Vec3(0f, 0f, 0.05f);
                    for (int i = 0; i < starRays; i++)
                    {
                        float a = (float)(i * 2.0 * Math.PI / starRays);
                        float aSpread = 0.28f;
                        float rOut = 0.60f + (float)_rng.NextDouble() * 0.22f;
                        float rInner = 0.18f;

                        Vec3 pL = new Vec3((float)Math.Cos(a - aSpread) * rInner, (float)Math.Sin(a - aSpread) * rInner, 0.06f);
                        Vec3 pR = new Vec3((float)Math.Cos(a + aSpread) * rInner, (float)Math.Sin(a + aSpread) * rInner, 0.06f);
                        Vec3 pTip = new Vec3((float)Math.Cos(a) * rOut, (float)Math.Sin(a) * rOut, 0.14f);

                        mesh.AddTriangleWithVertexColors(starCenter, pL, pTip, uv0, uv0, uv0, coreColor, midColor, tipColor, lockHandle);
                        mesh.AddTriangleWithVertexColors(starCenter, pTip, pR, uv0, uv0, uv0, coreColor, tipColor, midColor, lockHandle);
                    }
                }
                finally
                {
                    mesh.UnlockEditDataWrite(lockHandle);
                }

                mesh.UpdateBoundingBox();
                mesh.Color = 0xFFFF9A28u;
                return mesh;
            }
            catch
            {
                return null;
            }
        }

        private void SpawnEnhancedWhiteSmoke(Vec3 muzzlePos, Vec3 shotPos, Vec3 forward, Mat3 flashBasis)
        {
            Scene scene = Mission?.Scene;
            if (scene == null)
                return;

            try
            {
                Vec3 cameraPos = scene.LastFinalRenderCameraPosition;
                float distSq = muzzlePos.DistanceSquared(cameraPos);

                if (distSq > MaxVisibleDistanceSq)
                    return;

                bool fullDetail = distSq <= FullDetailDistanceSq;

                // Жесткий лимит активных облаков для защиты FPS при массовом залпе
                while (_activeClouds.Count >= MaxActiveClouds)
                {
                    DisposeCloud(_activeClouds[0]);
                    _activeClouds.RemoveAt(0);
                }

                GameEntity cloudEntity = GameEntity.CreateEmpty(scene, false, false, false);
                if (cloudEntity == null)
                    return;

                MatrixFrame entityFrame = new MatrixFrame(Mat3.Identity, muzzlePos);
                cloudEntity.SetGlobalFrame(in entityFrame, true);

                SmokeCloudInstance instance = new SmokeCloudInstance
                {
                    CloudEntity = cloudEntity,
                    Age = 0f,
                    FlashMeshDuration = 0.16f,
                    FlameBrakeTime = 0.065f,
                    FlameEmitDuration = 0.11f,
                    FlameCutoffDuration = 0.24f,
                    SteamEmitDuration = 0.38f,
                    FlashLightDuration = 0.15f,
                    FlashLightPeakIntensity = 300f,
                    HangBrakeTime = 0.24f,
                    MaxLifetime = fullDetail ? 9.5f : 7.0f,
                    FlashMeshHidden = true,
                    FlameBraked = false,
                    FlameStopped = true,
                    FlameHidden = true,
                    SteamStopped = !fullDetail,
                    FlashLightOff = !fullDetail,
                    HangBrakeApplied = false
                };

                // =========================================================================
                // 1. КРУПНОЕ (2x) СИММЕТРИЧНОЕ ДУЛЬНОЕ ПЛАМЯ СТРОГО ПО ОСИ СТВОЛА (vx=0, vy=0)
                //    Без psys_burning_projectile_default_coll_small (его impact_flames имеет local=false и world vx=+10, уводя огонь вбок!)
                // =========================================================================
                if (EnableEnhancedMuzzleFlash)
                {
                    // Слой 1А: Мгновенный процедурный 3D-факел + звезда в 0-й кадр (вдоль +Z = barrelForward)
                    Mesh flashMesh = CreateMuzzleFlashMesh();
                    if (flashMesh != null)
                    {
                        GameEntity flashMeshEntity = GameEntity.CreateEmpty(scene, false, false, false);
                        if (flashMeshEntity != null)
                        {
                            Mat3 rolledBasis = flashBasis;
                            rolledBasis.RotateAboutUp((float)(_rng.NextDouble() * Math.PI * 2.0));
                            MatrixFrame baseMeshFrame = new MatrixFrame(rolledBasis, muzzlePos);
                            MatrixFrame startFrame = baseMeshFrame;
                            startFrame.Scale(new Vec3(0.70f, 0.70f, 0.70f));

                            flashMeshEntity.AddMesh(flashMesh, true);
                            flashMeshEntity.SetGlobalFrame(in startFrame, true);

                            instance.FlashMeshEntity = flashMeshEntity;
                            instance.FlashMesh = flashMesh;
                            instance.FlashMeshBaseFrame = baseMeshFrame;
                            instance.FlashMeshHidden = false;
                        }
                    }

                    // Слой 1Б: Живой язык чистого кузнечного пламени (psys_game_blacksmith_flame: local=true, vx=0, vy=0, vz=+0.35, 0 дыма!)
                    // Выстроен строго по центру ствола от среза до +1.45м в масштабе 2.1x
                    float[] flameSteps = fullDetail ? new float[] { 0.06f, 0.36f, 0.72f, 1.10f, 1.48f } : new float[] { 0.15f, 0.75f };
                    for (int s = 0; s < flameSteps.Length; s++)
                    {
                        MatrixFrame jetFrame = new MatrixFrame(flashBasis, forward * flameSteps[s]);
                        float stepScale = s == 0 ? 2.20f : (2.10f - s * 0.12f);
                        jetFrame.Scale(new Vec3(stepScale, stepScale, stepScale));
                        ParticleSystem jetPs = ParticleSystem.CreateParticleSystemAttachedToEntity(BlacksmithFlameParticle, cloudEntity, ref jetFrame);
                        if (jetPs != null)
                        {
                            jetPs.SetDontRemoveFromEntity(true);
                            jetPs.SetRuntimeTimeMultiplier(2.8f);
                            jetPs.SetRuntimeEmissionRateMultiplier(fullDetail ? 8.5f : 4.5f);
                            instance.MuzzleFlames.Add(jetPs);
                            instance.FlameStopped = false;
                            instance.FlameHidden = false;
                        }
                    }

                    // Слой 1В: Сверхплотное симметричное огненное ядро (invoker: vx=0, vy=0, rate=150, life=0.40, масштаб 2.1x)
                    float[] coreSteps = fullDetail ? new float[] { 0.12f, 0.48f } : new float[] { 0.20f };
                    for (int c = 0; c < coreSteps.Length; c++)
                    {
                        MatrixFrame denseFrame = new MatrixFrame(flashBasis, forward * coreSteps[c]);
                        denseFrame.Scale(new Vec3(2.10f, 2.10f, 2.10f));
                        ParticleSystem densePs = ParticleSystem.CreateParticleSystemAttachedToEntity(DenseFlameJetParticle, cloudEntity, ref denseFrame);
                        if (densePs != null)
                        {
                            densePs.SetDontRemoveFromEntity(true);
                            densePs.SetRuntimeTimeMultiplier(2.6f);
                            densePs.SetRuntimeEmissionRateMultiplier(fullDetail ? 4.2f : 2.5f);
                            instance.MuzzleFlames.Add(densePs);
                            instance.FlameStopped = false;
                            instance.FlameHidden = false;
                        }
                    }

                    // Слой 1Г: Мгновенные горячие огненные искры (prt_shd_fire_1, alpha(0)=1.0, 0 дыма)
                    Mission.AddParticleSystemBurstByName(FierySparksParticle, new MatrixFrame(flashBasis, muzzlePos), false);
                    if (fullDetail)
                    {
                        Mission.AddParticleSystemBurstByName(FierySparksParticle, new MatrixFrame(flashBasis, muzzlePos + forward * 0.45f), false);
                        Mission.AddParticleSystemBurstByName(MuzzleSparksParticle, new MatrixFrame(flashBasis, muzzlePos + forward * 0.25f), false);
                    }
                }
                else
                {
                    Mission.AddParticleSystemBurstByName(MuzzleSparksParticle, new MatrixFrame(flashBasis, muzzlePos), false);
                }

                // Короткая яркая оранжевая вспышка освещения (с лимитом активных огней при массовых залпах)
                if (fullDetail && CountActiveFlashLights() < MaxActiveFlashLights)
                {
                    Light flashLight = Light.CreatePointLight(10.0f);
                    if (flashLight != null)
                    {
                        flashLight.LightColor = new Vec3(1.0f, 0.58f, 0.18f);
                        flashLight.Intensity = instance.FlashLightPeakIntensity;
                        flashLight.ShadowEnabled = false;
                        flashLight.Frame = new MatrixFrame(Mat3.Identity, forward * 0.15f);
                        cloudEntity.AddLight(flashLight);
                        instance.FlashLight = flashLight;
                    }
                    else
                    {
                        instance.FlashLightOff = true;
                    }
                }
                else
                {
                    instance.FlashLightOff = true;
                }

                // 2. ОБЪЕМНОЕ ОБЛАКО БЕЛОГО ДЫМА (28 сферических psys_smoke с сильным торможением ветра 0.08–0.11x)
                //    Старт конуса сдвинут на +0.28м от дульного среза, чтобы не перекрывать огненное ядро в момент выстрела.
                int burstCount = fullDetail ? 28 : 8;
                for (int i = 0; i < burstCount; i++)
                {
                    Vec3 localOffset;
                    if (fullDetail && i < 3)
                    {
                        localOffset = (shotPos - muzzlePos) - forward * 0.08f + new Vec3(0f, 0f, 0.06f) + RandomJitter(0.20f);
                    }
                    else
                    {
                        int coneIdx = fullDetail ? (i - 3) : i;
                        int coneTotal = fullDetail ? (burstCount - 3) : burstCount;
                        float t = coneTotal > 1 ? (float)coneIdx / (coneTotal - 1) : 0f;
                        float distAlong = 0.80f + (t * t * 0.45f + t * 0.55f) * 3.00f;
                        float coneSpread = 0.30f + t * 0.95f;
                        localOffset = forward * distAlong + RandomJitter(coneSpread);
                    }

                    MatrixFrame localFrame = new MatrixFrame(Mat3.Identity, localOffset);
                    ParticleSystem ps = ParticleSystem.CreateParticleSystemAttachedToEntity(WhiteSmokeBurstParticle, cloudEntity, ref localFrame);
                    if (ps != null)
                    {
                        ps.SetDontRemoveFromEntity(true);
                        ps.SetRuntimeTimeMultiplier(1.0f);
                        instance.SlowSmokeBursts.Add(ps);
                    }
                }

                // 3. ЯРКО-БЕЛЫЙ ПОДСЛОЙ (12 эмиттеров prt_steam_bath: diffuse_multiplier=3.0, wind_effect=0.30)
                //    Высветляет облако до молочно-белого порохового дыма, начиная с +0.35м перед огненным ядром
                if (fullDetail)
                {
                    for (int w = 0; w < 12; w++)
                    {
                        float wt = w / 11f;
                        Vec3 steamOffset = forward * (0.85f + wt * 2.45f) + RandomJitter(0.40f + wt * 0.55f);
                        MatrixFrame steamFrame = new MatrixFrame(Mat3.Identity, steamOffset);
                        ParticleSystem steamPs = ParticleSystem.CreateParticleSystemAttachedToEntity(BrightWhiteSteamParticle, cloudEntity, ref steamFrame);
                        if (steamPs != null)
                        {
                            steamPs.SetDontRemoveFromEntity(true);
                            steamPs.SetRuntimeTimeMultiplier(1.0f);
                            steamPs.SetRuntimeEmissionRateMultiplier(7.0f);
                            instance.BrightWhiteSteams.Add(steamPs);
                        }
                    }
                }

                if (instance.SlowSmokeBursts.Count == 0 && instance.MuzzleFlames.Count == 0)
                {
                    cloudEntity.Remove(0);
                    return;
                }

                _activeClouds.Add(instance);
            }
            catch
            {
                // Не допускаем сбоя клиента при проблемах с нативной сценой или отсутствием эффекта
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (GameNetwork.IsDedicatedServer || _activeClouds.Count == 0 || dt <= 0f || Mission == null || Mission.CurrentState != Mission.State.Continuing)
                return;

            for (int i = _activeClouds.Count - 1; i >= 0; i--)
            {
                SmokeCloudInstance cloud = _activeClouds[i];
                cloud.Age += dt;

                // 0. Анимация и гашение 3D-меша огненного факела (0.00–0.022с рост, 0.022–0.095с затухание и скрытие)
                if (!cloud.FlashMeshHidden && IsEntityValid(cloud.FlashMeshEntity))
                {
                    if (cloud.Age >= cloud.FlashMeshDuration)
                    {
                        cloud.FlashMeshHidden = true;
                        cloud.FlashMeshEntity.SetVisibilityExcludeParents(false);
                    }
                    else
                    {
                        float t = cloud.Age / cloud.FlashMeshDuration;
                        float scale = t < 0.23f
                            ? 0.45f + (t / 0.23f) * 0.70f
                            : 1.15f - ((t - 0.23f) / 0.77f) * 0.60f;
                        MatrixFrame animFrame = cloud.FlashMeshBaseFrame;
                        animFrame.Scale(new Vec3(scale, scale, scale));
                        cloud.FlashMeshEntity.SetGlobalFrame(in animFrame, true);

                        if (cloud.FlashMesh != null)
                        {
                            float alphaFade = t < 0.30f ? 1.0f : (1.0f - (t - 0.30f) / 0.70f);
                            uint alphaByte = (uint)Math.Max(0, Math.Min(255, (int)(255f * alphaFade * alphaFade)));
                            cloud.FlashMesh.SetColorAlpha(alphaByte);
                        }
                    }
                }

                // 1. Торможение времени огня с 0.065с (гасит снос пламени ветром вбок) + отсечка эмиссии и гашение
                if (!cloud.FlameBraked && cloud.Age >= cloud.FlameBrakeTime)
                {
                    cloud.FlameBraked = true;
                    for (int f = 0; f < cloud.MuzzleFlames.Count; f++)
                    {
                        cloud.MuzzleFlames[f]?.SetRuntimeTimeMultiplier(0.45f);
                    }
                }

                if (!cloud.FlameStopped && cloud.Age >= cloud.FlameEmitDuration)
                {
                    cloud.FlameStopped = true;
                    for (int f = 0; f < cloud.MuzzleFlames.Count; f++)
                    {
                        cloud.MuzzleFlames[f]?.SetRuntimeEmissionRateMultiplier(0f);
                    }
                }

                if (!cloud.FlameHidden && cloud.Age >= cloud.FlameCutoffDuration)
                {
                    cloud.FlameHidden = true;
                    for (int f = 0; f < cloud.MuzzleFlames.Count; f++)
                    {
                        cloud.MuzzleFlames[f]?.SetEnable(false);
                    }
                }

                // 2. Плавное затухание и выключение оранжевой вспышки света (~0.095с)
                if (!cloud.FlashLightOff && cloud.FlashLight != null)
                {
                    if (cloud.Age >= cloud.FlashLightDuration)
                    {
                        cloud.FlashLightOff = true;
                        cloud.FlashLight.Intensity = 0f;
                        cloud.FlashLight.SetVisibility(false);
                    }
                    else
                    {
                        float lt = 1.0f - (cloud.Age / cloud.FlashLightDuration);
                        cloud.FlashLight.Intensity = cloud.FlashLightPeakIntensity * lt * lt;
                    }
                }

                // 3. Отсечка и замедление ярко-белого подслоя prt_steam_bath (к 0.38с)
                if (!cloud.SteamStopped && cloud.Age >= cloud.SteamEmitDuration)
                {
                    cloud.SteamStopped = true;
                    for (int w = 0; w < cloud.BrightWhiteSteams.Count; w++)
                    {
                        ParticleSystem steamPs = cloud.BrightWhiteSteams[w];
                        if (steamPs != null)
                        {
                            steamPs.SetRuntimeEmissionRateMultiplier(0f);
                            steamPs.SetRuntimeTimeMultiplier(0.35f);
                        }
                    }
                }

                // 4. Аэродинамическое «зависание» на 0.24с: сильное торможение (~10x),
                //    чтобы дым почти не уносило ветром в сторону
                if (!cloud.HangBrakeApplied && cloud.Age >= cloud.HangBrakeTime)
                {
                    cloud.HangBrakeApplied = true;
                    for (int s = 0; s < cloud.SlowSmokeBursts.Count; s++)
                    {
                        ParticleSystem ps = cloud.SlowSmokeBursts[s];
                        if (ps != null)
                        {
                            ps.SetRuntimeTimeMultiplier(0.08f + (float)_rng.NextDouble() * 0.03f);
                        }
                    }
                }

                // 5. Естественное удаление после полного растворения всех частиц (либо по жесткому таймауту)
                bool timedOut = cloud.Age >= cloud.MaxLifetime;
                bool allDone = cloud.FlashMeshHidden && cloud.FlameStopped && cloud.FlameHidden && cloud.SteamStopped && cloud.FlashLightOff && cloud.HangBrakeApplied && !HasAnyAliveParticles(cloud);

                if (timedOut || allDone)
                {
                    DisposeCloud(cloud);
                    _activeClouds.RemoveAt(i);
                }
            }
        }

        private static bool HasAnyAliveParticles(SmokeCloudInstance cloud)
        {
            for (int i = 0; i < cloud.SlowSmokeBursts.Count; i++)
            {
                ParticleSystem ps = cloud.SlowSmokeBursts[i];
                if (ps != null && ps.HasAliveParticles())
                    return true;
            }
            for (int i = 0; i < cloud.BrightWhiteSteams.Count; i++)
            {
                ParticleSystem ps = cloud.BrightWhiteSteams[i];
                if (ps != null && ps.HasAliveParticles())
                    return true;
            }
            return false;
        }

        private static bool IsEntityValid(GameEntity entity)
        {
            return entity != null && entity.Pointer != UIntPtr.Zero && entity.Scene != null;
        }

        private void DisposeCloud(SmokeCloudInstance cloud)
        {
            if (cloud == null || Mission == null || Mission.CurrentState != Mission.State.Continuing)
                return;

            try
            {
                if (IsEntityValid(cloud.FlashMeshEntity))
                {
                    cloud.FlashMeshEntity.Remove(0);
                }
            }
            catch { }

            try
            {
                if (IsEntityValid(cloud.CloudEntity))
                {
                    cloud.CloudEntity.Remove(0);
                }
            }
            catch { }
        }

        private void ResetSmokeStateOnly()
        {
            // При OnClearScene / OnEndMission / OnRemoveBehavior нативная сцена уже очищается движком;
            // вызов Remove(0) в этот момент опасен (риск 0xC0000005), поэтому очищаем только управляемый список и кэш.
            _activeClouds.Clear();
            _cachedFlashMaterial = null;
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            ResetSmokeStateOnly();
        }

        protected override void OnEndMission()
        {
            ResetSmokeStateOnly();
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            ResetSmokeStateOnly();
            base.OnRemoveBehavior();
        }
    }
}
