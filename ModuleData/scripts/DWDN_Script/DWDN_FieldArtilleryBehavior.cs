using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using DWDN_Script.Common.Network;
using Path = System.IO.Path;

namespace DWDN_Script
{
    /// <summary>
    /// Новая Полевая Артиллерия DWDN (DWDN_FieldArtilleryBehavior).
    /// Полностью пересобрана по каноническому ТЗ:
    /// 1. ВЫБОР ЧЕРТЕЖА: При спавне инженера выбирается передвижная баллиста на тележке или онагр.
    /// 2. ПОХОДНОЕ ПОЛОЖЕНИЕ:
    ///    - Баллиста смонтирована внутри ванильной деревянной тележки (bd_cart_a).
    ///    - Онагр стоит прямо на грунте на своих собственных колесах (без платформы).
    ///    - К орудию привязана тягловая лошадь, на которую можно сесть верхом.
    ///    - Лошадь движется СТРОГО ШАГОМ, без бега и галопа (SetMaximumSpeedLimit).
    ///    - Орудие на тележке / на колесах плавно следует за лошадью на сцепке.
    ///    - В походном виде стрельба отключена, снаряды свернуты.
    /// 3. БОЕВОЕ ПОЛОЖЕНИЕ ПО КЛАВИШЕ [F]:
    ///    - На позиции боец подходит к упряжи и нажимает [F] «Отцепить коня».
    ///    - Конь отцепляется от тележки.
    ///    - Рядом с орудием на грунте спавнятся его боеприпасы (куча камней для онагра / болты для баллисты).
    ///    - Орудие становится полноценной стационарной осадной артиллерией с рабочими станциями наводки, зарядки и огня.
    /// </summary>
    public class DWDN_FieldArtilleryBehavior : MissionLogic
    {
        public static int MaxCannonsPerTeam { get; set; } = 3;
        public static float InteractRadius { get; private set; } = 3.5f;
        public static float DestroyCooldownSeconds { get; private set; } = 25.0f;

        public class EngineTypeSpec
        {
            public string Id = "mangonel";
            public string PrefabName = "mangonel_a";
            public string TriggerItemId = "dwdn_order_mangonel";
            public string Name = "Передвижной Онагр";
            public bool UsesCart = false; // Онагр на своих колесах!
            public float MaxHp = 1400f;
        }

        public class EngineInstance
        {
            public int Id;
            public EngineTypeSpec Spec;
            public BattleSideEnum TeamSide;
            public string OwnerPlayerId = "";
            public MissionObjectId RootObjectId;
            public MatrixFrame Frame;

            public Agent HorseAgent;
            public GameEntity RootEntity;
            public GameEntity CartEntity;
            public GameEntity AmmoPileEntity;

            public RangedSiegeWeapon SiegeWeapon;
            public DestructableComponent Destructable;
            public List<StandingPoint> StandingPoints = new List<StandingPoint>();
            public List<MissionObjectId> DescendantObjectIds = new List<MissionObjectId>();

            public bool IsHitched = true;
            public bool IsCombatState = false;
            public bool IsDestroyed = false;
            public float SpawnTime = 0f;
            public float DestroyedTime = 0f;
            public float DestroyTimer => DestroyedTime;
            public Agent PushingAgent => HorseAgent;

            public int SavedAmmo = 25;
            public Vec3 Position => Frame.origin;
            public Vec3 HitchPosition => Frame.origin + (Frame.rotation.f * 2.2f) + new Vec3(0f, 0f, 0.45f);

            public void SetCombatState(bool combat)
            {
                IsCombatState = combat;
                IsHitched = !combat;

                if (HorseAgent != null && HorseAgent.IsActive())
                {
                    try
                    {
                        if (combat)
                        {
                            // Освобождаем ограничение скорости у коня при распряжке
                            HorseAgent.SetMaximumSpeedLimit(-1f, false);
                        }
                        else
                        {
                            // На марше конь движется строго шагом
                            HorseAgent.SetMaximumSpeedLimit(2.2f, false);
                        }
                        HorseAgent.UpdateAgentProperties();
                    }
                    catch { }
                }

                // Включаем или выключаем кучу снарядов
                if (AmmoPileEntity != null)
                {
                    try
                    {
                        AmmoPileEntity.SetVisibilityExcludeParents(combat);
                    }
                    catch { }
                }

                // Активируем или деактивируем осадные машины (UsableMachine)
                try
                {
                    if (combat)
                    {
                        SiegeWeapon?.Activate();
                        SetSiegeWeaponLoadAmmoPointActive(SiegeWeapon, true);
                        var stonePile = AmmoPileEntity?.GetFirstScriptOfType<UsableMachine>();
                        stonePile?.Activate();
                    }
                    else
                    {
                        if (SiegeWeapon != null)
                        {
                            SavedAmmo = SiegeWeapon.AmmoCount;
                        }
                        SiegeWeapon?.Deactivate();
                        var stonePile = AmmoPileEntity?.GetFirstScriptOfType<UsableMachine>();
                        stonePile?.Deactivate();
                    }
                }
                catch { }

                // Собираем все стояночные посты (StandingPoint) орудия и боеприпасов
                var allStandingPoints = new HashSet<StandingPoint>();
                if (StandingPoints != null)
                {
                    foreach (var sp in StandingPoints)
                    {
                        if (sp != null) allStandingPoints.Add(sp);
                    }
                }
                if (AmmoPileEntity != null)
                {
                    try
                    {
                        var pileObjects = new List<MissionObject>();
                        CollectMissionObjectsRecursive(AmmoPileEntity, pileObjects);
                        foreach (var sp in pileObjects.OfType<StandingPoint>())
                        {
                            if (sp != null) allStandingPoints.Add(sp);
                        }
                    }
                    catch { }
                }
                if (RootEntity != null)
                {
                    try
                    {
                        var rootObjects = new List<MissionObject>();
                        CollectMissionObjectsRecursive(RootEntity, rootObjects);
                        foreach (var sp in rootObjects.OfType<StandingPoint>())
                        {
                            if (sp != null) allStandingPoints.Add(sp);
                        }
                    }
                    catch { }
                }

                // Включаем или выключаем подножки орудия и снарядов с правильным API движка и сетевой синхронизацией
                foreach (var sp in allStandingPoints)
                {
                    if (sp == null) continue;
                    try
                    {
                        if (combat)
                        {
                            sp.SetEnabled(false);
                            sp.IsDisabledForPlayers = false;
                            sp.IsDeactivated = false;
                            SetStandingPointNeutral(sp);
                            if (GameNetwork.IsServer)
                            {
                                sp.SetIsDisabledForPlayersSynched(false);
                                sp.SetIsDeactivatedSynched(false);
                            }
                        }
                        else
                        {
                            sp.IsDisabledForPlayers = true;
                            sp.IsDeactivated = true;
                            if (GameNetwork.IsServer)
                            {
                                sp.SetIsDisabledForPlayersSynched(true);
                                sp.SetIsDeactivatedSynched(true);
                            }
                            sp.SetDisabled(false);
                        }
                    }
                    catch { }
                }

                if (combat && SiegeWeapon != null)
                {
                    try
                    {
                        SetSiegeWeaponDefaultSide(SiegeWeapon, TeamSide);
                        int ammoCount = SavedAmmo > 0 ? SavedAmmo : 25;
                        SiegeWeapon.StartingAmmoCount = ammoCount;
                        SiegeWeapon.SetAmmo(ammoCount);
                    }
                    catch { }
                }
            }
        }

        private static readonly Dictionary<string, EngineTypeSpec> _engineSpecs = new Dictionary<string, EngineTypeSpec>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<EngineInstance> _activeEngines = new List<EngineInstance>();
        private static readonly Dictionary<string, float> _playerLastEngineDestroyed = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<int> _spawnedAgentIndices = new HashSet<int>();
        private static readonly object _lockObject = new object();

        private static int _nextEngineId = 1;
        private static bool _configLoaded = false;
        private static bool _networkHandlersRegistered = false;
        private float _tickTimer = 0f;

        public static IReadOnlyList<EngineInstance> ActiveEngines => _activeEngines;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            LoadConfig();
            RegisterNetworkHandlers();
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            // Примечание: спавн орудия производится через DWDN_ServerLoadoutBehavior.TryApplyLoadoutToAgent
            // после полной настройки экипировки и чертежей агента, чтобы избежать двойного спавна и гонки состояний.
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if (affectedAgent != null)
            {
                lock (_lockObject)
                {
                    _spawnedAgentIndices.Remove(affectedAgent.Index);
                }
            }
        }

        public override void EarlyStart()
        {
            base.EarlyStart();
            RegisterNetworkHandlers();
        }

        public override void AfterStart()
        {
            base.AfterStart();
            RegisterNetworkHandlers();
        }

        public override void OnRemoveBehavior()
        {
            UnregisterNetworkHandlers();
            CleanupAllCannons();
            base.OnRemoveBehavior();
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            CleanupAllCannons();
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            CleanupAllCannons();
        }

        // =========================================================================
        // РЕГИСТРАЦИЯ СЕТЕВЫХ ОБРАБОТЧИКОВ
        // =========================================================================

        private static void RegisterNetworkHandlers()
        {
            if (_networkHandlersRegistered) return;

            if (GameNetwork.IsServer)
            {
                DWDN_NetworkHelper.RegisterClientMessageHandler<RequestDeployArtilleryMessage>(HandleClientDeployRequest);
                DWDN_NetworkHelper.RegisterClientMessageHandler<RequestToggleArtilleryHitchMessage>(HandleClientToggleHitchRequest);
                _networkHandlersRegistered = true;
                Debug.Print("[DWDN Artillery] Server message handlers registered.");
            }
            else if (GameNetwork.IsClient)
            {
                DWDN_NetworkHelper.RegisterServerMessageHandler<SyncArtilleryDescendantsMessage>(HandleServerSyncDescendants);
                DWDN_NetworkHelper.RegisterServerMessageHandler<SetArtilleryCombatStateMessage>(HandleServerCombatState);
                _networkHandlersRegistered = true;
                Debug.Print("[DWDN Artillery] Client message handlers registered.");
            }
        }

        private static void UnregisterNetworkHandlers()
        {
            if (!_networkHandlersRegistered) return;

            if (GameNetwork.IsServer)
            {
                DWDN_NetworkHelper.UnregisterClientMessageHandler<RequestDeployArtilleryMessage>(HandleClientDeployRequest);
                DWDN_NetworkHelper.UnregisterClientMessageHandler<RequestToggleArtilleryHitchMessage>(HandleClientToggleHitchRequest);
            }
            else if (GameNetwork.IsClient)
            {
                DWDN_NetworkHelper.UnregisterServerMessageHandler<SyncArtilleryDescendantsMessage>(HandleServerSyncDescendants);
                DWDN_NetworkHelper.UnregisterServerMessageHandler<SetArtilleryCombatStateMessage>(HandleServerCombatState);
            }
            _networkHandlersRegistered = false;
        }

        // =========================================================================
        // КОНФИГУРАЦИЯ ОРУДИЙ
        // =========================================================================

        public static void LoadConfig()
        {
            if (_configLoaded) return;
            _configLoaded = true;

            // 1. Передвижной Онагр (mangonel_a): стоит прямо на земле на своих колесах
            var mangonelSpec = new EngineTypeSpec
            {
                Id = "mangonel",
                PrefabName = "mangonel_a",
                TriggerItemId = "dwdn_order_mangonel",
                Name = "Передвижной Онагр",
                UsesCart = false,
                MaxHp = 1400f
            };
            RegisterSpec(mangonelSpec);

            // 2. Передвижная Баллиста (ballista_a): стоит прямо на земле на своих опорах (UsesCart = false)
            var ballistaSpec = new EngineTypeSpec
            {
                Id = "ballista",
                PrefabName = "ballista_a",
                TriggerItemId = "dwdn_order_ballista",
                Name = "Передвижная Баллиста",
                UsesCart = false,
                MaxHp = 1100f
            };
            RegisterSpec(ballistaSpec);

            // Загрузка MaxCannonsPerTeam из artillery_config.xml если есть
            string[] paths = new string[]
            {
                Path.Combine(BasePath.Name, "Modules", "DWDN_TRY", "ModuleData", "artillery_config.xml"),
                Path.Combine(BasePath.Name, "Modules", "DaewdenM", "ModuleData", "artillery_config.xml")
            };

            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    try
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.Load(p);
                        var root = doc.DocumentElement;
                        if (root == null) continue;

                        var limitAttr = root.Attributes["max_cannons_per_team"] ?? root.Attributes["MaxCannonsPerTeam"];
                        if (limitAttr != null && int.TryParse(limitAttr.Value, out int maxTeam))
                        {
                            MaxCannonsPerTeam = maxTeam;
                        }
                        break;
                    }
                    catch { }
                }
            }
        }

        private static void RegisterSpec(EngineTypeSpec spec)
        {
            _engineSpecs[spec.Id] = spec;
            if (!string.IsNullOrEmpty(spec.TriggerItemId))
            {
                _engineSpecs[spec.TriggerItemId] = spec;
            }
        }

        public static EngineTypeSpec GetSpecForType(string typeOrTrigger)
        {
            if (string.IsNullOrEmpty(typeOrTrigger)) return null;
            if (_engineSpecs.TryGetValue(typeOrTrigger, out var spec)) return spec;
            if (typeOrTrigger.IndexOf("ballista", StringComparison.OrdinalIgnoreCase) >= 0) return _engineSpecs["ballista"];
            if (typeOrTrigger.IndexOf("mangonel", StringComparison.OrdinalIgnoreCase) >= 0 || typeOrTrigger.IndexOf("onager", StringComparison.OrdinalIgnoreCase) >= 0) return _engineSpecs["mangonel"];
            return null;
        }

        public static bool CanSpawnCannonForTeam(BattleSideEnum side, string playerId, out string reason)
        {
            reason = "";
            lock (_lockObject)
            {
                int count = _activeEngines.Count(e => e.TeamSide == side && !e.IsDestroyed);
                if (count >= MaxCannonsPerTeam)
                {
                    reason = $"Лимит артиллерии команды исчерпан ({count}/{MaxCannonsPerTeam}).";
                    return false;
                }

                if (!string.IsNullOrEmpty(playerId))
                {
                    if (_activeEngines.Any(e => e.OwnerPlayerId == playerId && !e.IsDestroyed))
                    {
                        reason = "У вас уже есть активное орудие на поле боя.";
                        return false;
                    }

                    if (_playerLastEngineDestroyed.TryGetValue(playerId, out float destroyedAt))
                    {
                        float passed = Mission.Current?.CurrentTime ?? 0f - destroyedAt;
                        if (passed < DestroyCooldownSeconds)
                        {
                            int left = (int)Math.Ceiling(DestroyCooldownSeconds - passed);
                            reason = $"Орудие на перезарядке/кулдауне: осталось {left} сек.";
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        // =========================================================================
        // СПАВН ОРУДИЙ НА СЕРВЕРЕ (ЛОШАДЬ + ТЕЛЕЖКА + ОРУДИЕ)
        // =========================================================================

        public static EngineInstance SpawnEngine(Mission mission, EngineTypeSpec spec, BattleSideEnum teamSide, Vec3 position, Vec2 heading, string ownerPlayerId)
        {
            if (mission?.Scene == null || spec == null) return null;

            lock (_lockObject)
            {
                int engineId = _nextEngineId++;

                float groundZ = position.z;
                try
                {
                    if (mission.Scene.ContainsTerrain)
                    {
                        float tZ = mission.Scene.GetTerrainHeight(position.AsVec2);
                        if (Math.Abs(tZ - position.z) < 3.0f)
                        {
                            groundZ = tZ;
                        }
                        else
                        {
                            mission.Scene.GetHeightAtPoint(position.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref groundZ);
                        }
                    }
                    else
                    {
                        mission.Scene.GetHeightAtPoint(position.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref groundZ);
                    }
                }
                catch
                {
                    mission.Scene.GetHeightAtPoint(position.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref groundZ);
                }
                position.z = groundZ;

                MatrixFrame rootFrame = MatrixFrame.Identity;
                rootFrame.origin = position;
                float yaw = (float)Math.Atan2(heading.x, heading.y);
                rootFrame.rotation.RotateAboutUp(yaw);

                // 1. СПАВН ТЕЛЕЖКИ (для баллисты)
                GameEntity cartEntity = null;
                if (spec.UsesCart)
                {
                    try
                    {
                        cartEntity = GameEntity.CreateEmpty(mission.Scene);
                        cartEntity.AddMultiMesh(MetaMesh.GetCopy("bd_cart_a"));
                        cartEntity.SetFrame(ref rootFrame);
                    }
                    catch (Exception ex)
                    {
                        Debug.Print($"[DWDN Artillery] Cart spawn warning: {ex.Message}");
                    }
                }

                // 2. СПАВН ОСАДНОГО ОРУДИЯ
                MatrixFrame weaponFrame = rootFrame;
                if (spec.UsesCart)
                {
                    // В кузове тележки баллиста приподнята на высоту кузова
                    weaponFrame.origin.z += 0.40f;
                }

                GameEntity root = GameEntity.Instantiate(mission.Scene, spec.PrefabName, weaponFrame, false);
                if (root == null)
                {
                    Debug.Print($"[DWDN Artillery] Critical Error: Failed to instantiate siege engine {spec.PrefabName}!");
                    return null;
                }

                // Находим встроенную кучу снарядов projectile_pile
                GameEntity ammoPile = FindChildByNameRecursive(root, "projectile_pile");

                // Подавление Dynamic Navmesh ДО вызова CallScriptCallbacks / OnInit:
                // Предотвращает попытку нативного движка импортировать 'ballista_a_dnm' и пересчитывать
                // навигационную сетку на каждом тике перемещения орудия (приводившую к аллокации 17 ГБ и крашу).
                SuppressDynamicNavmeshRecursive(root);

                // Запускаем CallScriptCallbacks ровно один раз для полной инициализации компонентов движком
                root.CallScriptCallbacks(true);

                // Рекурсивно собираем все MissionObject ПОСЛЕ вызова CallScriptCallbacks,
                // чтобы зафиксировать истинные сетевые ID, зарегистрированные сервером в Mission!
                List<MissionObject> allObjects = new List<MissionObject>();
                CollectMissionObjectsRecursive(root, allObjects);

                List<MissionObjectId> descendantIds = new List<MissionObjectId>();
                for (int i = 0; i < allObjects.Count; i++)
                {
                    descendantIds.Add(allObjects[i].Id);
                }

                RangedSiegeWeapon siegeWeapon = allObjects.OfType<RangedSiegeWeapon>().FirstOrDefault();
                DestructableComponent destructable = allObjects.OfType<DestructableComponent>().FirstOrDefault();
                List<StandingPoint> standingPoints = allObjects.OfType<StandingPoint>().ToList();

                if (siegeWeapon != null)
                {
                    SetSiegeWeaponDefaultSide(siegeWeapon, teamSide);
                    siegeWeapon.StartingAmmoCount = 25;
                    siegeWeapon.SetAmmo(25);
                }

                for (int i = 0; i < allObjects.Count; i++)
                {
                    var mo = allObjects[i];
                    if (_navMeshPrefabNameField != null)
                    {
                        try { _navMeshPrefabNameField.SetValue(mo, ""); } catch { }
                    }

                    if (mo is StandingPoint sp)
                    {
                        SetStandingPointNeutral(sp);
                        // В походном виде блокируем использование
                        sp.IsDisabledForPlayers = true;
                        sp.SetDisabled(true);
                    }
                    if (mo is DestructableComponent dc)
                    {
                        dc.BattleSide = BattleSideEnum.None;
                        if (spec.MaxHp > 0f)
                        {
                            dc.MaxHitPoint = spec.MaxHp;
                            dc.HitPoint = spec.MaxHp;
                        }
                    }
                }

                // В походном виде куча снарядов скрыта
                if (ammoPile != null)
                {
                    try
                    {
                        ammoPile.SetVisibilityExcludeParents(false);
                    }
                    catch { }
                }

                // 3. СПАВН ТЯГЛОВОЙ ЛОШАДИ ВПЕРЕДИ ОРУДИЯ
                Vec3 horsePos = position + new Vec3(heading.x * 3.8f, heading.y * 3.8f, 0f);
                float hZ = horsePos.z;
                try
                {
                    if (mission.Scene.ContainsTerrain)
                    {
                        float tZ = mission.Scene.GetTerrainHeight(horsePos.AsVec2);
                        if (Math.Abs(tZ - horsePos.z) < 3.0f)
                        {
                            hZ = tZ;
                        }
                        else
                        {
                            mission.Scene.GetHeightAtPoint(horsePos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref hZ);
                        }
                    }
                    else
                    {
                        mission.Scene.GetHeightAtPoint(horsePos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref hZ);
                    }
                }
                catch
                {
                    mission.Scene.GetHeightAtPoint(horsePos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref hZ);
                }
                horsePos.z = hZ;

                Vec2 horseHeading = heading;
                ItemObject horseItem = MBObjectManager.Instance?.GetObject<ItemObject>("mp_vlandia_horse")
                                    ?? MBObjectManager.Instance?.GetObject<ItemObject>("sumpter_horse")
                                    ?? MBObjectManager.Instance?.GetObject<ItemObject>("mule");
                ItemObject harnessItem = MBObjectManager.Instance?.GetObject<ItemObject>("mp_light_harness")
                                      ?? MBObjectManager.Instance?.GetObject<ItemObject>("mp_chain_barding");

                Agent horseAgent = null;
                try
                {
                    horseAgent = mission.SpawnMonster(
                        new ItemRosterElement(horseItem, 1),
                        new ItemRosterElement(harnessItem, 1),
                        horsePos,
                        horseHeading,
                        (int)teamSide
                    );

                    if (horseAgent != null)
                    {
                        // ЖЕСТКИЙ ЗАПРЕТ БЕГА И ГАЛОПА: ТОЛЬКО ШАГ
                        horseAgent.SetMaximumSpeedLimit(2.2f, false);
                        horseAgent.UpdateAgentProperties();
                    }
                }
                catch (Exception ex)
                {
                    Debug.Print($"[DWDN Artillery] Horse spawn exception: {ex.Message}");
                }

                MissionObject rootMissionObject = allObjects.FirstOrDefault();

                EngineInstance instance = new EngineInstance
                {
                    Id = engineId,
                    Spec = spec,
                    TeamSide = teamSide,
                    OwnerPlayerId = ownerPlayerId ?? "",
                    RootObjectId = rootMissionObject?.Id ?? MissionObjectId.Invalid,
                    Frame = rootFrame,
                    HorseAgent = horseAgent,
                    RootEntity = root,
                    CartEntity = cartEntity,
                    AmmoPileEntity = ammoPile,
                    SiegeWeapon = siegeWeapon,
                    Destructable = destructable,
                    StandingPoints = standingPoints,
                    DescendantObjectIds = descendantIds,
                    IsHitched = true,
                    IsCombatState = false,
                    SpawnTime = mission?.CurrentTime ?? 0f
                };

                _activeEngines.Add(instance);

                // 4. СЕТЕВАЯ РАССЫЛКА ВСЕМ КЛИЕНТАМ
                if (GameNetwork.IsServer)
                {
                    GameNetwork.BeginBroadcastModuleEvent();
                    GameNetwork.WriteMessage(new SyncArtilleryDescendantsMessage(
                        engineId,
                        instance.RootObjectId,
                        spec.PrefabName,
                        (int)teamSide,
                        rootFrame,
                        horseAgent?.Index ?? -1,
                        spec.UsesCart,
                        descendantIds
                    ));
                    GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.AddToMissionRecord, null);
                }

                Debug.Print($"[DWDN Artillery] Spawned {spec.Name} (ID: {engineId}, Cart: {spec.UsesCart}) with Draft Horse (Index: {horseAgent?.Index})");
                return instance;
            }
        }

        // =========================================================================
        // ОБРАБОТЧИК СЕТЕВОЙ СИНХРОНИЗАЦИИ НА КЛИЕНТЕ
        // =========================================================================

        private static void HandleServerSyncDescendants(SyncArtilleryDescendantsMessage message)
        {
            if (message == null || Mission.Current?.Scene == null) return;

            lock (_lockObject)
            {
                if (_activeEngines.Any(e => e.Id == message.EngineId)) return;

                var spec = GetSpecForType(message.PrefabName);
                if (spec == null)
                {
                    spec = new EngineTypeSpec
                    {
                        Id = message.PrefabName,
                        PrefabName = message.PrefabName,
                        Name = message.PrefabName.Contains("ballista") ? "Передвижная Баллиста на тележке" : "Передвижной Онагр",
                        UsesCart = message.UsesCart
                    };
                }

                MatrixFrame rootFrame = message.Frame;

                // 1. Клиентская тележка (для баллисты)
                GameEntity cartEntity = null;
                if (message.UsesCart)
                {
                    try
                    {
                        cartEntity = GameEntity.CreateEmpty(Mission.Current.Scene);
                        cartEntity.AddMultiMesh(MetaMesh.GetCopy("bd_cart_a"));
                        cartEntity.SetFrame(ref rootFrame);
                    }
                    catch { }
                }

                // 2. Орудие
                MatrixFrame weaponFrame = rootFrame;
                if (message.UsesCart)
                {
                    weaponFrame.origin.z += 0.40f;
                }

                GameEntity root = null;
                try
                {
                    root = GameEntity.Instantiate(Mission.Current.Scene, message.PrefabName, weaponFrame, false);
                }
                catch { }

                if (root == null) return;

                GameEntity ammoPile = FindChildByNameRecursive(root, "projectile_pile");

                // Подавление Dynamic Navmesh ДО вызова CallScriptCallbacks / OnInit на клиенте
                SuppressDynamicNavmeshRecursive(root);

                try
                {
                    root.CallScriptCallbacks(true);
                }
                catch { }

                List<MissionObject> allObjects = new List<MissionObject>();
                CollectMissionObjectsRecursive(root, allObjects);

                var receivedIds = message.DescendantObjectIds;
                for (int i = 0; i < allObjects.Count && i < receivedIds.Count; i++)
                {
                    allObjects[i].Id = receivedIds[i];
                }

                RangedSiegeWeapon siegeWeapon = allObjects.OfType<RangedSiegeWeapon>().FirstOrDefault();
                if (siegeWeapon != null)
                {
                    SetSiegeWeaponDefaultSide(siegeWeapon, (BattleSideEnum)message.TeamSide);
                }
                DestructableComponent destructable = allObjects.OfType<DestructableComponent>().FirstOrDefault();
                List<StandingPoint> standingPoints = allObjects.OfType<StandingPoint>().ToList();

                for (int i = 0; i < allObjects.Count; i++)
                {
                    var mo = allObjects[i];
                    if (_navMeshPrefabNameField != null)
                    {
                        try { _navMeshPrefabNameField.SetValue(mo, ""); } catch { }
                    }

                    if (mo is StandingPoint sp)
                    {
                        SetStandingPointNeutral(sp);
                        sp.IsDisabledForPlayers = true;
                        sp.SetDisabled(true);
                    }
                    if (mo is DestructableComponent dc)
                    {
                        dc.BattleSide = BattleSideEnum.None;
                    }
                }

                if (ammoPile != null)
                {
                    try { ammoPile.SetVisibilityExcludeParents(false); } catch { }
                }

                Agent horseAgent = null;
                if (message.HorseAgentIndex >= 0)
                {
                    horseAgent = Mission.Current?.Agents?.FirstOrDefault(a => a.IsMount && a.Index == message.HorseAgentIndex)
                                 ?? Mission.Current?.Agents?.FirstOrDefault(a => a.IsMount);
                    if (horseAgent != null && horseAgent.IsActive())
                    {
                        try
                        {
                            horseAgent.SetMaximumSpeedLimit(2.2f, false);
                            horseAgent.UpdateAgentProperties();
                        }
                        catch { }
                    }
                }

                EngineInstance instance = new EngineInstance
                {
                    Id = message.EngineId,
                    Spec = spec,
                    TeamSide = (BattleSideEnum)message.TeamSide,
                    RootObjectId = message.RootObjectId,
                    Frame = rootFrame,
                    HorseAgent = horseAgent,
                    RootEntity = root,
                    CartEntity = cartEntity,
                    AmmoPileEntity = ammoPile,
                    SiegeWeapon = siegeWeapon,
                    Destructable = destructable,
                    StandingPoints = standingPoints,
                    DescendantObjectIds = message.DescendantObjectIds,
                    IsHitched = true,
                    IsCombatState = false,
                    SpawnTime = Mission.Current?.CurrentTime ?? 0f
                };

                _activeEngines.Add(instance);
                Debug.Print($"[DWDN Artillery Client] Mirrored {spec.Name} (ID: {instance.Id}, Cart: {message.UsesCart})");
            }
        }

        // =========================================================================
        // ПЕРЕВОД В БОЕВОЕ ПОЛОЖЕНИЕ И СЦЕПКА / ОТЦЕПКА
        // =========================================================================

        private static bool HandleClientDeployRequest(NetworkCommunicator peer, RequestDeployArtilleryMessage message)
        {
            if (peer == null || message == null) return false;
            Agent agent = peer.ControlledAgent;
            if (agent == null || !agent.IsActive()) return false;

            lock (_lockObject)
            {
                var engine = _activeEngines.FirstOrDefault(e => e.Id == message.EngineId);
                if (engine == null || engine.IsDestroyed || !engine.IsHitched) return false;

                float distSq = agent.Position.DistanceSquared(engine.Position);
                if (distSq > (InteractRadius + 2.0f) * (InteractRadius + 2.0f)) return false;

                DeployEngineToCombat(engine);
                return true;
            }
        }

        private static bool HandleClientToggleHitchRequest(NetworkCommunicator peer, RequestToggleArtilleryHitchMessage message)
        {
            if (peer == null || message == null) return false;
            Agent agent = peer.ControlledAgent;
            if (agent == null || !agent.IsActive()) return false;

            lock (_lockObject)
            {
                var engine = _activeEngines.FirstOrDefault(e => e.Id == message.EngineId);
                if (engine == null || engine.IsDestroyed) return false;

                // 1. Проверка прав: создатель орудия, возница, класс артиллериста или союзник
                bool isOwner = !string.IsNullOrEmpty(engine.OwnerPlayerId) &&
                               (engine.OwnerPlayerId == (peer.VirtualPlayer?.Id.ToString() ?? "") ||
                                engine.OwnerPlayerId == peer.UserName);
                bool isArtillerist = agent.Character?.StringId?.IndexOf("artillerist", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isDriver = (agent.MountAgent != null && engine.HorseAgent == agent.MountAgent);
                bool isFriendly = !GameNetwork.IsMultiplayer || agent.Team == null || agent.Team.Side == engine.TeamSide;

                if (!isOwner && !isArtillerist && !isDriver && !isFriendly) return false;

                // 2. Проверка дистанции бойца до дышла (для возницы дистанция гарантирована)
                if (!isDriver)
                {
                    float dist = agent.Position.Distance(engine.HitchPosition);
                    if (dist > 3.8f) return false;
                }

                // 3. Выполнение сцепки или отцепки
                if (message.Attach)
                {
                    if (engine.IsHitched) return false;

                    Agent horseAgent = null;
                    if (message.HorseAgentIndex >= 0)
                    {
                        horseAgent = Mission.Current?.Agents?.FirstOrDefault(a => a.IsMount && a.Index == message.HorseAgentIndex);
                    }
                    if (horseAgent == null || !horseAgent.IsMount || !horseAgent.IsActive() || (horseAgent.RiderAgent != null && horseAgent.RiderAgent != agent) || IsHorseHitched(horseAgent))
                    {
                        horseAgent = null;
                        // Fallback: ищем ближайшего свободного коня (включая трофейных коней врага!)
                        float closestDistSq = 4.0f * 4.0f;
                        try
                        {
                            var agents = Mission.Current?.Agents;
                            if (agents != null)
                            {
                                for (int i = 0; i < agents.Count; i++)
                                {
                                    var a = agents[i];
                                    if (a != null && a.IsMount && a.IsActive() && (a.RiderAgent == null || a.RiderAgent == agent) && !IsHorseHitched(a))
                                    {
                                        float dSq = a.Position.DistanceSquared(engine.HitchPosition);
                                        if (dSq <= closestDistSq)
                                        {
                                            closestDistSq = dSq;
                                            horseAgent = a;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    if (horseAgent == null) return false;

                    HitchEngineToHorse(engine, horseAgent);
                    return true;
                }
                else
                {
                    if (engine.IsCombatState) return false;
                    DeployEngineToCombat(engine);
                    return true;
                }
            }
        }

        public static bool IsHorseHitched(Agent horse)
        {
            if (horse == null) return false;
            lock (_lockObject)
            {
                for (int i = 0; i < _activeEngines.Count; i++)
                {
                    var e = _activeEngines[i];
                    if (!e.IsDestroyed && e.HorseAgent == horse)
                        return true;
                }
            }
            return false;
        }

        public static void HitchEngineToHorse(EngineInstance engine, Agent horseAgent)
        {
            if (engine == null || horseAgent == null || !horseAgent.IsActive()) return;

            lock (_lockObject)
            {
                engine.HorseAgent = horseAgent;
                engine.SetCombatState(false);

                if (GameNetwork.IsServer)
                {
                    GameNetwork.BeginBroadcastModuleEvent();
                    GameNetwork.WriteMessage(new SetArtilleryCombatStateMessage(engine.Id, false, engine.Frame, horseAgent.Index, engine.SavedAmmo));
                    GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.AddToMissionRecord, null);
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[DWDN Артиллерия] {engine.Spec.Name} запряжен в походную упряжь! Орудие переведено в походный режим.",
                    Colors.Green
                ));
            }
        }

        public static void DeployEngineToCombat(EngineInstance engine)
        {
            if (engine == null || engine.IsCombatState) return;

            lock (_lockObject)
            {
                engine.SetCombatState(true);
                engine.HorseAgent = null;

                if (GameNetwork.IsServer)
                {
                    GameNetwork.BeginBroadcastModuleEvent();
                    GameNetwork.WriteMessage(new SetArtilleryCombatStateMessage(engine.Id, true, engine.Frame, -1, engine.SavedAmmo));
                    GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.AddToMissionRecord, null);
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[DWDN Артиллерия] {engine.Spec.Name} отцеплен от коня и приведен в боевое положение! Снаряды выгружены.",
                    Colors.Green
                ));
            }
        }

        private static void HandleServerCombatState(SetArtilleryCombatStateMessage message)
        {
            if (message == null) return;

            lock (_lockObject)
            {
                var engine = _activeEngines.FirstOrDefault(e => e.Id == message.EngineId);
                if (engine == null) return;

                if (!message.IsCombatState && message.HorseAgentIndex >= 0)
                {
                    engine.HorseAgent = Mission.Current?.Agents?.FirstOrDefault(a => a.IsMount && a.Index == message.HorseAgentIndex)
                                        ?? Mission.Current?.Agents?.FirstOrDefault(a => a.IsMount);
                }

                engine.SavedAmmo = message.RemainingAmmo;
                engine.SetCombatState(message.IsCombatState);

                if (message.IsCombatState)
                {
                    engine.HorseAgent = null;
                }

                if (message.Frame.origin.LengthSquared > 0.01f)
                {
                    engine.Frame = message.Frame;
                    MatrixFrame f = message.Frame;
                    engine.RootEntity?.SetFrame(ref f);
                    engine.CartEntity?.SetFrame(ref f);
                }

                string msg = message.IsCombatState
                    ? $"[DWDN Артиллерия] {engine.Spec.Name} приведен в боевое положение!"
                    : $"[DWDN Артиллерия] {engine.Spec.Name} запряжен в походное положение!";
                InformationManager.DisplayMessage(new InformationMessage(msg, Colors.Green));
            }
        }

        // =========================================================================
        // МИССИОННЫЙ ТИК: ДВИЖЕНИЕ СЦЕПКИ И ИНТЕРАКТИВНОЕ ВЗАИМОДЕЙСТВИЕ
        // =========================================================================

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            _tickTimer += dt;
            if (_tickTimer < 0.033f) return;
            float stepDt = _tickTimer;
            _tickTimer = 0f;

            lock (_lockObject)
            {
                for (int i = _activeEngines.Count - 1; i >= 0; i--)
                {
                    var engine = _activeEngines[i];
                    if (engine.IsDestroyed) continue;

                    // 1. Проверка уничтожения
                    if (engine.Destructable != null && engine.Destructable.IsDestroyed)
                    {
                        engine.IsDestroyed = true;
                        engine.DestroyedTime = Mission.Current?.CurrentTime ?? 0f;
                        if (!string.IsNullOrEmpty(engine.OwnerPlayerId))
                        {
                            _playerLastEngineDestroyed[engine.OwnerPlayerId] = engine.DestroyedTime;
                        }
                        if (engine.HorseAgent != null && engine.HorseAgent.IsActive())
                        {
                            try
                            {
                                engine.HorseAgent.SetMaximumSpeedLimit(-1f, false);
                                engine.HorseAgent.UpdateAgentProperties();
                            }
                            catch { }
                        }
                        engine.HorseAgent = null;
                        continue;
                    }

                    // Защита от катапультирования в небеса / провала под текстуры
                    if (engine.Frame.origin.z > 1500f || engine.Frame.origin.z < -200f || float.IsNaN(engine.Frame.origin.z))
                    {
                        engine.IsDestroyed = true;
                        engine.DestroyedTime = Mission.Current?.CurrentTime ?? 0f;
                        if (!string.IsNullOrEmpty(engine.OwnerPlayerId))
                        {
                            _playerLastEngineDestroyed[engine.OwnerPlayerId] = engine.DestroyedTime;
                        }
                        if (engine.HorseAgent != null && engine.HorseAgent.IsActive())
                        {
                            try
                            {
                                engine.HorseAgent.SetMaximumSpeedLimit(-1f, false);
                                engine.HorseAgent.UpdateAgentProperties();
                            }
                            catch { }
                        }
                        engine.HorseAgent = null;
                        continue;
                    }

                    // 2. ДВИЖЕНИЕ В ПОХОДНОМ ВИДЕ (БУКСИРОВКА ЛОШАДЬЮ)
                    if (engine.IsHitched)
                    {
                        try
                        {
                            if (engine.HorseAgent != null && engine.HorseAgent.IsActive())
                            {
                                // Если у тягловой лошади нет всадника (артиллерист спешился или еще не сел), орудие не перемещается
                                if (engine.HorseAgent.RiderAgent == null)
                                {
                                    continue;
                                }

                                // Обновляем позицию тележки/орудия за лошадью
                                Vec3 horsePos = engine.HorseAgent.Position;
                                if (float.IsNaN(horsePos.x) || float.IsInfinity(horsePos.x) ||
                                    float.IsNaN(horsePos.y) || float.IsInfinity(horsePos.y) ||
                                    float.IsNaN(horsePos.z) || float.IsInfinity(horsePos.z))
                                {
                                    continue;
                                }

                                Vec2 horseFwd = engine.HorseAgent.Frame.rotation.f.AsVec2.Normalized();
                                if (horseFwd.LengthSquared < 0.01f || float.IsNaN(horseFwd.x) || float.IsInfinity(horseFwd.x)) horseFwd = new Vec2(0f, 1f);

                                float hitchDistance = 3.6f;
                                Vec3 targetPos = horsePos - new Vec3(horseFwd.x * hitchDistance, horseFwd.y * hitchDistance, 0f);
                                targetPos.z = horsePos.z;
                                try
                                {
                                    if (Mission.Current?.Scene != null)
                                    {
                                        float gz = targetPos.z;
                                        if (Mission.Current.Scene.ContainsTerrain)
                                        {
                                            gz = Mission.Current.Scene.GetTerrainHeight(targetPos.AsVec2);
                                        }
                                        else
                                        {
                                            Mission.Current.Scene.GetHeightAtPoint(targetPos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref gz);
                                        }
                                        if (!float.IsNaN(gz) && !float.IsInfinity(gz) && Math.Abs(gz - horsePos.z) < 3.5f)
                                        {
                                            targetPos.z = gz;
                                        }
                                    }
                                }
                                catch { }

                                if (float.IsNaN(targetPos.x) || float.IsInfinity(targetPos.x) ||
                                    float.IsNaN(targetPos.y) || float.IsInfinity(targetPos.y) ||
                                    float.IsNaN(targetPos.z) || float.IsInfinity(targetPos.z))
                                {
                                    continue;
                                }

                                Vec2 toHorse = (horsePos.AsVec2 - targetPos.AsVec2).Normalized();
                                if (toHorse.LengthSquared < 0.01f || float.IsNaN(toHorse.x) || float.IsInfinity(toHorse.x)) toHorse = horseFwd;

                                MatrixFrame f = MatrixFrame.Identity;
                                f.origin = targetPos;
                                f.rotation.RotateAboutUp((float)Math.Atan2(toHorse.x, toHorse.y));

                                // Защита от передачи невалидных координат в нативный движок C++ (предотвращает OOM 34GB)
                                if (float.IsNaN(f.origin.x) || float.IsInfinity(f.origin.x) ||
                                    float.IsNaN(f.origin.y) || float.IsInfinity(f.origin.y) ||
                                    float.IsNaN(f.origin.z) || float.IsInfinity(f.origin.z))
                                {
                                    continue;
                                }

                                // Если дистанция изменилась, сдвигаем тележку и орудие
                                if (engine.Position.DistanceSquared(targetPos) > 0.04f || Math.Abs(engine.Position.z - targetPos.z) > 0.05f)
                                {
                                    engine.Frame = f;

                                    if (engine.CartEntity != null)
                                    {
                                        engine.CartEntity.SetFrame(ref f);
                                    }

                                    if (engine.RootEntity != null)
                                    {
                                        MatrixFrame weaponF = f;
                                        if (engine.Spec.UsesCart)
                                        {
                                            weaponF.origin.z += 0.40f;
                                        }
                                        engine.RootEntity.SetFrame(ref weaponF);
                                    }
                                }
                            }
                            else if (engine.HorseAgent != null && !engine.HorseAgent.IsActive())
                            {
                                // Если тягловый конь погиб на марше, автоматически развертываем орудие и оповещаем сеть
                                DeployEngineToCombat(engine);
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        // =========================================================================
        // СПАВН ПРИ ВХОДЕ АРТИЛЛЕРИСТА
        // =========================================================================

        public static void CheckAndSpawnArtilleryForAgent(Agent agent)
        {
            if (agent == null || !agent.IsHuman || !agent.IsActive()) return;
            if (!GameNetwork.IsServer && GameNetwork.IsMultiplayer) return;

            // Защитный реестр: если для данного агента уже производился спавн орудия в этой жизни - блокируем повторный вызов!
            lock (_lockObject)
            {
                if (_spawnedAgentIndices.Contains(agent.Index))
                {
                    return;
                }
            }

            LoadConfig();

            string charId = agent.Character?.StringId ?? "";
            bool isArtillerist = charId.IndexOf("artillerist", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isArtillerist && agent.MissionPeer != null)
            {
                var classDiv = MultiplayerClassDivisions.GetMPHeroClassForCharacter(agent.Character);
                if (classDiv != null && classDiv.StringId.IndexOf("artillerist", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isArtillerist = true;
                }
            }

            bool wantsBallista = false;
            bool hasArtilleryBlueprint = false;

            // 1. Проверяем кастомный лодаут DWDN (выбор оружия в интерфейсе кастомизации)
            try
            {
                var selection = DWDN_Script.Server.Behaviors.DWDN_ServerLoadoutBehavior.GetSelectionForAgent(agent);
                if (selection != null)
                {
                    string[] candidates = new[] { selection.Weapon1Id, selection.Weapon0Id, selection.Weapon2Id, selection.Weapon3Id };
                    foreach (var c in candidates)
                    {
                        if (!string.IsNullOrEmpty(c))
                        {
                            if (c.IndexOf("ballista", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                wantsBallista = true;
                                hasArtilleryBlueprint = true;
                                break;
                            }
                            if (c.IndexOf("mangonel", StringComparison.OrdinalIgnoreCase) >= 0 || c.IndexOf("onager", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                hasArtilleryBlueprint = true;
                            }
                        }
                    }
                }
            }
            catch { }

            // 2. Проверяем физические предметы экипировки (dwdn_order_ballista / dwdn_order_mangonel)
            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.ExtraWeaponSlot; i++)
            {
                var item = agent.Equipment[i].Item;
                if (item != null)
                {
                    if (item.StringId.IndexOf("ballista", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        wantsBallista = true;
                        hasArtilleryBlueprint = true;
                        break;
                    }
                    if (item.StringId.IndexOf("mangonel", StringComparison.OrdinalIgnoreCase) >= 0 || item.StringId.IndexOf("onager", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasArtilleryBlueprint = true;
                    }
                }
            }

            // 3. Проверяем нативные перки дивизиона
            if (agent.MissionPeer != null)
            {
                try
                {
                    if (agent.MissionPeer.SelectedPerks != null)
                    {
                        foreach (var perk in agent.MissionPeer.SelectedPerks)
                        {
                            if (perk != null && perk.Name != null)
                            {
                                string pName = perk.Name.ToString();
                                if (pName.IndexOf("ballista", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    wantsBallista = true;
                                    hasArtilleryBlueprint = true;
                                    break;
                                }
                                if (pName.IndexOf("onager", StringComparison.OrdinalIgnoreCase) >= 0 || pName.IndexOf("mangonel", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    hasArtilleryBlueprint = true;
                                }
                            }
                        }
                    }

                    if (isArtillerist && !wantsBallista)
                    {
                        int pIdx = agent.MissionPeer.GetSelectedPerkIndexWithPerkListIndex(agent.MissionPeer.SelectedTroopIndex, 1);
                        if (pIdx == 1)
                        {
                            wantsBallista = true;
                            hasArtilleryBlueprint = true;
                        }
                        else if (pIdx == 0)
                        {
                            hasArtilleryBlueprint = true;
                        }
                    }
                }
                catch { }
            }

            // Если нет чертежей осадного орудия — спавнить ничего не нужно (артиллерист воюет пешим)
            if (!hasArtilleryBlueprint) return;

            EngineTypeSpec chosenSpec = wantsBallista ? GetSpecForType("ballista") : GetSpecForType("mangonel");
            if (chosenSpec == null) return;

            string playerId = "";
            if (agent.MissionPeer != null)
            {
                playerId = agent.MissionPeer.Peer?.Id.ToString()
                    ?? agent.MissionPeer.GetNetworkPeer()?.VirtualPlayer?.Id.ToString()
                    ?? agent.MissionPeer.Name;
            }
            else if (agent.IsPlayerControlled)
            {
                playerId = GameNetwork.MyPeer?.VirtualPlayer?.Id.ToString() ?? "local_player";
            }
            else
            {
                playerId = $"bot_{agent.Index}";
            }

            BattleSideEnum side = agent.Team?.Side ?? BattleSideEnum.Attacker;

            if (!CanSpawnCannonForTeam(side, playerId, out string reason))
            {
                // Если причина отказа - орудие от прошлой жизни игрока, утилизируем его и разрешаем спавн
                bool canProceed = false;
                lock (_lockObject)
                {
                    var existingEngine = _activeEngines.FirstOrDefault(e => e.OwnerPlayerId == playerId && !e.IsDestroyed);
                    if (existingEngine != null)
                    {
                        // ЗАЩИТА: Если орудие создано менее 5 секунд назад, НЕ уничтожать его!
                        float age = (Mission.Current?.CurrentTime ?? 0f) - existingEngine.SpawnTime;
                        if (age < 5.0f)
                        {
                            return; // Орудие уже существует и только что создано
                        }

                        if (existingEngine.SiegeWeapon == null || existingEngine.SiegeWeapon.PilotAgent == null)
                        {
                            existingEngine.IsDestroyed = true;
                            existingEngine.DestroyedTime = Mission.Current?.CurrentTime ?? 0f;
                            try
                            {
                                if (existingEngine.Destructable != null)
                                {
                                    existingEngine.Destructable.HitPoint = 0f;
                                }
                                if (existingEngine.HorseAgent != null && existingEngine.HorseAgent.IsActive())
                                {
                                    existingEngine.HorseAgent.FadeOut(false, true);
                                }
                                if (existingEngine.RootEntity != null)
                                {
                                    existingEngine.RootEntity.FadeOut(0.5f, false);
                                }
                                if (existingEngine.CartEntity != null)
                                {
                                    existingEngine.CartEntity.FadeOut(0.5f, false);
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            existingEngine.OwnerPlayerId = "";
                        }

                        _playerLastEngineDestroyed.Remove(playerId);
                        canProceed = true;
                    }
                }

                if (!canProceed || !CanSpawnCannonForTeam(side, playerId, out reason))
                {
                    if (agent.IsPlayerControlled && !string.IsNullOrEmpty(reason))
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[DWDN Артиллерия] {reason}", Colors.Red));
                    }
                    return;
                }
            }

            Vec2 forward = agent.LookDirection.AsVec2.Normalized();
            if (forward.LengthSquared < 0.01f) forward = new Vec2(0f, 1f);

            Vec3 spawnPos = agent.Position + new Vec3(forward.x * 4.5f, forward.y * 4.5f, 0f);
            var engine = SpawnEngine(Mission.Current, chosenSpec, side, spawnPos, forward, playerId);

            if (engine != null)
            {
                lock (_lockObject)
                {
                    _spawnedAgentIndices.Add(agent.Index);
                }

                if (engine.HorseAgent != null && agent.MountAgent == null)
                {
                    try { agent.Mount(engine.HorseAgent); } catch { }
                }

                if (agent.IsPlayerControlled)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[DWDN Артиллерия] Ваш {chosenSpec.Name} запряжен и готов к движению! На позиции нажмите [F], чтобы отцепить коня.",
                        Colors.Green
                    ));
                }
            }
        }

        // =========================================================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // =========================================================================

        public static EngineTypeSpec GetSpecForWeaponItem(string id) => GetSpecForType(id);

        private static readonly FieldInfo _navMeshPrefabNameField = typeof(MissionObject).GetField("NavMeshPrefabName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        /// <summary>
        /// Подавляет Dynamic Navmesh префабы (например, ballista_a_dnm у баллисты)
        /// предотвращая катастрофическую аллокацию памяти (17 ГБ) на сервере и клиенте при движении орудия.
        /// </summary>
        private static void SuppressDynamicNavmeshRecursive(GameEntity entity)
        {
            if (entity == null) return;
            try
            {
                foreach (var mo in entity.GetScriptComponents<MissionObject>())
                {
                    if (mo != null && _navMeshPrefabNameField != null)
                    {
                        try
                        {
                            _navMeshPrefabNameField.SetValue(mo, "");
                        }
                        catch { }
                    }
                }

                foreach (var child in entity.GetChildren())
                {
                    SuppressDynamicNavmeshRecursive(child);
                }
            }
            catch { }
        }

        private static void CollectMissionObjectsRecursive(GameEntity entity, List<MissionObject> result)
        {
            if (entity == null) return;

            foreach (var mo in entity.GetScriptComponents<MissionObject>())
            {
                if (mo != null && !result.Contains(mo))
                {
                    result.Add(mo);
                }
            }

            foreach (var child in entity.GetChildren())
            {
                CollectMissionObjectsRecursive(child, result);
            }
        }

        private static GameEntity FindChildByNameRecursive(GameEntity entity, string name)
        {
            if (entity == null) return null;
            if (entity.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                return entity;

            foreach (var child in entity.GetChildren())
            {
                var found = FindChildByNameRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetStandingPointNeutral(StandingPoint sp)
        {
            if (sp == null) return;
            try
            {
                var sideField = typeof(StandingPoint).GetField("StandingPointSide", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (sideField != null)
                {
                    sideField.SetValue(sp, BattleSideEnum.None);
                }
            }
            catch { }
        }

        private static void SetSiegeWeaponDefaultSide(RangedSiegeWeapon weapon, BattleSideEnum side)
        {
            if (weapon == null) return;
            try
            {
                var sideField = typeof(RangedSiegeWeapon).GetField("DefaultSide", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (sideField != null)
                {
                    sideField.SetValue(weapon, side);
                }
            }
            catch { }
        }

        private static void SetSiegeWeaponLoadAmmoPointActive(RangedSiegeWeapon weapon, bool active)
        {
            if (weapon == null) return;
            try
            {
                var method = typeof(RangedSiegeWeapon).GetMethod("SetActivationLoadAmmoPoint", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                method?.Invoke(weapon, new object[] { active });
            }
            catch { }
        }

        public static void CleanupAllCannons()
        {
            lock (_lockObject)
            {
                foreach (var e in _activeEngines)
                {
                    try { e.RootEntity?.Remove(0); } catch { }
                    try { e.CartEntity?.Remove(0); } catch { }
                    try { e.HorseAgent?.FadeOut(true, true); } catch { }
                }
                _activeEngines.Clear();
                _playerLastEngineDestroyed.Clear();
                _spawnedAgentIndices.Clear();
            }
        }

        public static void SyncEnginesToPeer(NetworkCommunicator peer)
        {
            if (peer == null || !peer.IsConnectionActive || !GameNetwork.IsServer) return;

            lock (_lockObject)
            {
                foreach (var engine in _activeEngines)
                {
                    if (engine.IsDestroyed) continue;

                    GameNetwork.BeginModuleEventAsServer(peer);
                    GameNetwork.WriteMessage(new SyncArtilleryDescendantsMessage(
                        engine.Id,
                        engine.RootObjectId,
                        engine.Spec.PrefabName,
                        (int)engine.TeamSide,
                        engine.Frame,
                        engine.HorseAgent?.Index ?? -1,
                        engine.Spec.UsesCart,
                        engine.DescendantObjectIds
                    ));
                    GameNetwork.EndModuleEventAsServer();

                    if (engine.IsCombatState)
                    {
                        GameNetwork.BeginModuleEventAsServer(peer);
                        GameNetwork.WriteMessage(new SetArtilleryCombatStateMessage(engine.Id, true, engine.Frame, -1, engine.SavedAmmo));
                        GameNetwork.EndModuleEventAsServer();
                    }
                }
            }
        }
    }
}
