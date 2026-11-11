using System;
using System.IO;
using System.Reflection;
using Path = System.IO.Path;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using DWDN_Script.Common.Duchies;
using DWDN_Script.Server.Behaviors;
#if !SERVER
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;
using DWDN_Script.Client.Behaviors;
#endif

namespace DWDN_Script
{
    public class SubModule : MBSubModuleBase
    {
#if !SERVER
        private static TaleWorlds.TwoDimension.Texture _injectedFactionTexture;
        private static TaleWorlds.TwoDimension.Texture _injectedLoadingTexture1;
        private static TaleWorlds.TwoDimension.Texture _injectedLoadingTexture2;
        private static TaleWorlds.TwoDimension.Texture _injectedLoadingTexture3;
        private static bool _factionTextureLoaded = false;
        private static bool _loadingTextureLoaded = false;
#endif

        private static bool _assemblyResolverRegistered = false;

        private static void RegisterAssemblyResolver()
        {
            if (_assemblyResolverRegistered) return;
            _assemblyResolverRegistered = true;

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    string shortName = new AssemblyName(args.Name).Name;
                    string basePath = BasePath.Name ?? @"e:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord";

                    // 1. Check game client bin
                    string binDir = Path.Combine(basePath, "bin", "Win64_Shipping_Client");
                    string p1 = Path.Combine(binDir, shortName + ".dll");
                    if (File.Exists(p1)) return Assembly.LoadFrom(p1);

                    // 2. Check game server bin
                    string srvBinDir = Path.Combine(basePath, "bin", "Win64_Shipping_Server");
                    string p1s = Path.Combine(srvBinDir, shortName + ".dll");
                    if (File.Exists(p1s)) return Assembly.LoadFrom(p1s);

                    // 3. Check Multiplayer module bin
                    string mpBin = Path.Combine(basePath, "Modules", "Multiplayer", "bin", "Win64_Shipping_Client", shortName + ".dll");
                    if (File.Exists(mpBin)) return Assembly.LoadFrom(mpBin);

                    string mpBinSrv = Path.Combine(basePath, "Modules", "Multiplayer", "bin", "Win64_Shipping_Server", shortName + ".dll");
                    if (File.Exists(mpBinSrv)) return Assembly.LoadFrom(mpBinSrv);

                    // 4. Check Native module bin
                    string natBin = Path.Combine(basePath, "Modules", "Native", "bin", "Win64_Shipping_Client", shortName + ".dll");
                    if (File.Exists(natBin)) return Assembly.LoadFrom(natBin);
                }
                catch
                {
                }
                return null;
            };
        }

        protected override void OnSubModuleLoad()
        {
            RegisterAssemblyResolver();
            base.OnSubModuleLoad();

            DWDN_DuchyManager.Initialize();
#if !SERVER
            if (!GameNetwork.IsDedicatedServer)
            {
                EnsureTexturesLoaded();
                if (LoadingWindow.IsLoadingWindowActive)
                {
                    FixAllLoadingScreens();
                }
            }
#endif
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            DWDN_DuchyManager.Initialize();
#if !SERVER
            if (!GameNetwork.IsDedicatedServer)
            {
                EnsureTexturesLoaded();
                if (LoadingWindow.IsLoadingWindowActive)
                {
                    FixAllLoadingScreens();
                }
            }
#endif

            InformationManager.DisplayMessage(new InformationMessage(
                "DWDN Module успешно загружен!",
                Colors.Green
            ));
        }

        public override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            base.OnBeforeMissionBehaviorInitialize(mission);
            AddMissionBehaviors(mission);
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            AddMissionBehaviors(mission);
        }

        private void AddMissionBehaviors(Mission mission)
        {
            if (mission == null) return;

            if (mission.GetMissionBehavior<DWDN_FirearmsBehavior>() == null)
            {
                mission.AddMissionBehavior(new DWDN_FirearmsBehavior());
            }

            if (mission.GetMissionBehavior<DWDN_BannerAuraBehavior>() == null)
            {
                mission.AddMissionBehavior(new DWDN_BannerAuraBehavior());
            }

            if (mission.GetMissionBehavior<DWDN_ServerLoadoutBehavior>() == null)
            {
                mission.AddMissionBehavior(new DWDN_ServerLoadoutBehavior());
            }

#if !SERVER
            if (!GameNetwork.IsDedicatedServer)
            {
                if (mission.GetMissionBehavior<DWDN_ClientMissionComponent>() == null)
                {
                    mission.AddMissionBehavior(new DWDN_ClientMissionComponent());
                }
            }
#endif
        }

#if !SERVER
        private static bool IsTwoDimTextureAlive(TaleWorlds.TwoDimension.Texture tex)
        {
            if (tex == null) return false;
            try
            {
                if (!tex.IsValid) return false;
                if (tex.PlatformTexture is EngineTexture et)
                {
                    if (et.Texture == null || !et.Texture.IsValid) return false;
                }
                return tex.Width > 0 && tex.Height > 0;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            if (GameNetwork.IsDedicatedServer)
                return;

            try
            {
                if (!IsTwoDimTextureAlive(_injectedFactionTexture))
                    _factionTextureLoaded = false;

                if (!IsTwoDimTextureAlive(_injectedLoadingTexture1) ||
                    !IsTwoDimTextureAlive(_injectedLoadingTexture2) ||
                    !IsTwoDimTextureAlive(_injectedLoadingTexture3))
                {
                    _loadingTextureLoaded = false;
                }

                if (!_factionTextureLoaded || !_loadingTextureLoaded)
                {
                    EnsureTexturesLoaded();
                }
                else if (UIResourceManager.SpriteData != null)
                {
                    // Если движок подгрузил pack0.tpac поверх наших категорий (например, при открытии Intermission или Loading),
                    // мгновенно восстанавливаем наши чистые текстуры в SpriteSheets
                    if (_injectedFactionTexture != null)
                    {
                        if (!UIResourceManager.SpriteData.SpriteCategories.TryGetValue("dwdn_faction", out var fCat) ||
                            fCat == null || fCat.SpriteSheets == null || fCat.SpriteSheets.Count == 0 ||
                            fCat.SpriteSheets[0] != _injectedFactionTexture)
                        {
                            InjectIntoSpriteData(UIResourceManager.SpriteData, "dwdn_faction", @"StdAssets\FactionIcons\LargeIcons\dwdn", @"StdAssets\FactionIcons\SmallIcons\dwdn", _injectedFactionTexture);
                        }
                    }

                    if (_injectedLoadingTexture1 != null)
                    {
                        if (!UIResourceManager.SpriteData.SpriteCategories.TryGetValue("dwdn_loading", out var lCat) ||
                            lCat == null || lCat.SpriteSheets == null || lCat.SpriteSheets.Count == 0 ||
                            lCat.SpriteSheets[0] != _injectedLoadingTexture1)
                        {
                            InjectAllLoadingSprites(UIResourceManager.SpriteData);
                        }
                    }
                }

                if (LoadingWindow.IsLoadingWindowActive)
                {
                    FixAllLoadingScreens();
                }

                // Исправляем щиты на всех экранах интерфейса (включая выбор команды и экран смены карты Intermission)
                if (ScreenManager.SortedLayers != null)
                {
                    for (int i = 0; i < ScreenManager.SortedLayers.Count; i++)
                    {
                        if (ScreenManager.SortedLayers[i] is GauntletLayer gl && gl.UIContext?.Root != null)
                        {
                            FixFactionBanners(gl.UIContext.Root);
                            if (IsIntermissionLayer(gl))
                            {
                                CleanIntermissionWidgets(gl.UIContext.Root);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void FixAllLoadingScreens()
        {
            try
            {
                // 1. Hook TaleWorlds.Engine.LoadingWindow.LoadingWindowManager directly
                var mgr = LoadingWindow.LoadingWindowManager;
                if (mgr != null)
                {
                    Type mgrType = mgr.GetType();

                    // Access private GauntletLayer _gauntletLayer
                    FieldInfo layerField = mgrType.GetField("_gauntletLayer", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (layerField != null)
                    {
                        if (layerField.GetValue(mgr) is GauntletLayer gl && gl.UIContext?.Root != null)
                        {
                            FixFactionBanners(gl.UIContext.Root);
                            FixLoadingWidgets(gl.UIContext.Root);
                        }
                    }

                    // Access private LoadingWindowViewModel _loadingWindowViewModel
                    FieldInfo vmField = mgrType.GetField("_loadingWindowViewModel", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (vmField != null)
                    {
                        object vm = vmField.GetValue(mgr);
                        if (vm != null)
                        {
                            PropertyInfo prop = vm.GetType().GetProperty("LoadingImageName");
                            if (prop != null)
                            {
                                string curr = prop.GetValue(vm) as string;
                                if (string.IsNullOrEmpty(curr) || curr.StartsWith("loading_") || !curr.StartsWith("dwdn_"))
                                {
                                    prop.SetValue(vm, "dwdn_screen_load_1");
                                }
                            }
                        }
                    }

                    // Access private SpriteCategory _sploadingCategory
                    FieldInfo catField = mgrType.GetField("_sploadingCategory", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (catField != null && _injectedLoadingTexture1 != null)
                    {
                        if (catField.GetValue(mgr) is SpriteCategory cat)
                        {
                            InjectIntoSpriteCategoryDirect(cat);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("[DWDN EXCEPTION] FixAllLoadingScreens: " + ex);
            }
        }

        private static void EnsureTexturesLoaded()
        {
            EnsureFactionTexture();
            EnsureLoadingTexture();
        }

        private static void EnsureFactionTexture()
        {
            if (_factionTextureLoaded && IsTwoDimTextureAlive(_injectedFactionTexture))
            {
                if (UIResourceManager.SpriteData != null)
                {
                    InjectIntoSpriteData(UIResourceManager.SpriteData, "dwdn_faction", @"StdAssets\FactionIcons\LargeIcons\dwdn", @"StdAssets\FactionIcons\SmallIcons\dwdn", _injectedFactionTexture);
                }
                return;
            }

            try
            {
                string moduleDir = Path.Combine(BasePath.Name, "Modules", "DWDN_TRY");
                string pngPath = Path.Combine(moduleDir, "GUI", "SpriteSheets", "dwdn_faction", "dwdn_faction_1.png");
                if (!File.Exists(pngPath))
                {
                    pngPath = Path.Combine(moduleDir, "GUI", "SpriteSheets", "dwdn_faction", "dwdn_faction.png");
                }

                if (!File.Exists(pngPath))
                {
                    Debug.Print("[DWDN ERROR] Faction PNG not found at: " + pngPath);
                    return;
                }

                TaleWorlds.Engine.Texture engineTex = LoadEngineTexture(pngPath, "Modules/DWDN_TRY/GUI/SpriteSheets/dwdn_faction");
                if (engineTex != null)
                {
                    engineTex.SetTextureAsAlwaysValid();
                    engineTex.PreloadTexture(true);

                    EngineTexture platformTex = new EngineTexture(engineTex);
                    _injectedFactionTexture = new TaleWorlds.TwoDimension.Texture(platformTex);
                    _factionTextureLoaded = true;

                    if (UIResourceManager.SpriteData != null)
                    {
                        InjectIntoSpriteData(UIResourceManager.SpriteData, "dwdn_faction", @"StdAssets\FactionIcons\LargeIcons\dwdn", @"StdAssets\FactionIcons\SmallIcons\dwdn", _injectedFactionTexture);
                    }
                    Debug.Print("[DWDN] Faction texture loaded successfully! Size: " + engineTex.Width + "x" + engineTex.Height);
                }
            }
            catch (Exception ex)
            {
                Debug.Print("[DWDN EXCEPTION] EnsureFactionTexture failed: " + ex);
            }
        }

        private static void EnsureLoadingTexture()
        {
            if (_loadingTextureLoaded &&
                IsTwoDimTextureAlive(_injectedLoadingTexture1) &&
                IsTwoDimTextureAlive(_injectedLoadingTexture2) &&
                IsTwoDimTextureAlive(_injectedLoadingTexture3))
            {
                if (UIResourceManager.SpriteData != null)
                {
                    InjectAllLoadingSprites(UIResourceManager.SpriteData);
                }
                return;
            }

            try
            {
                string moduleDir = Path.Combine(BasePath.Name, "Modules", "DWDN_TRY");
                string pngPath1 = Path.Combine(moduleDir, "GUI", "SpriteSheets", "dwdn_loading", "dwdn_screen_load_1.png");
                if (!File.Exists(pngPath1))
                {
                    pngPath1 = Path.Combine(moduleDir, "GUI", "SpriteSheets", "dwdn_loading", "dwdn_screen_load.png");
                }

                string pngPath2 = Path.Combine(moduleDir, "GUI", "SpriteSheets", "dwdn_loading", "dwdn_screen_load_2.png");
                string pngPath3 = Path.Combine(moduleDir, "GUI", "SpriteSheets", "dwdn_loading", "dwdn_screen_load_3.png");

                if (File.Exists(pngPath1))
                {
                    TaleWorlds.Engine.Texture engineTex1 = LoadEngineTexture(pngPath1, "Modules/DWDN_TRY/GUI/SpriteSheets/dwdn_loading");
                    if (engineTex1 != null)
                    {
                        engineTex1.SetTextureAsAlwaysValid();
                        engineTex1.PreloadTexture(true);

                        EngineTexture platformTex1 = new EngineTexture(engineTex1);
                        _injectedLoadingTexture1 = new TaleWorlds.TwoDimension.Texture(platformTex1);
                    }
                }

                if (File.Exists(pngPath2))
                {
                    TaleWorlds.Engine.Texture engineTex2 = LoadEngineTexture(pngPath2, "Modules/DWDN_TRY/GUI/SpriteSheets/dwdn_loading");
                    if (engineTex2 != null)
                    {
                        engineTex2.SetTextureAsAlwaysValid();
                        engineTex2.PreloadTexture(true);

                        EngineTexture platformTex2 = new EngineTexture(engineTex2);
                        _injectedLoadingTexture2 = new TaleWorlds.TwoDimension.Texture(platformTex2);
                    }
                }

                if (File.Exists(pngPath3))
                {
                    TaleWorlds.Engine.Texture engineTex3 = LoadEngineTexture(pngPath3, "Modules/DWDN_TRY/GUI/SpriteSheets/dwdn_loading");
                    if (engineTex3 != null)
                    {
                        engineTex3.SetTextureAsAlwaysValid();
                        engineTex3.PreloadTexture(true);

                        EngineTexture platformTex3 = new EngineTexture(engineTex3);
                        _injectedLoadingTexture3 = new TaleWorlds.TwoDimension.Texture(platformTex3);
                    }
                }

                if (_injectedLoadingTexture1 != null)
                {
                    _loadingTextureLoaded = true;

                    if (UIResourceManager.SpriteData != null)
                    {
                        InjectAllLoadingSprites(UIResourceManager.SpriteData);
                    }
                    Debug.Print("[DWDN] Custom loading screens loaded successfully!");
                }
            }
            catch (Exception ex)
            {
                Debug.Print("[DWDN EXCEPTION] EnsureLoadingTexture failed: " + ex);
            }
        }

        private static TaleWorlds.Engine.Texture LoadEngineTexture(string fullPath, string relativeFolder)
        {
            TaleWorlds.Engine.Texture engineTex = null;

            // Method 1: CreateFromMemory
            try
            {
                if (File.Exists(fullPath))
                {
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    if (bytes != null && bytes.Length > 0)
                    {
                        engineTex = TaleWorlds.Engine.Texture.CreateFromMemory(bytes);
                        if (engineTex != null)
                        {
                            Debug.Print("[DWDN] Loaded via CreateFromMemory: " + Path.GetFileName(fullPath));
                            return engineTex;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("[DWDN] CreateFromMemory failed: " + ex.Message);
            }

            // Method 2: CreateTextureFromPath
            try
            {
                PlatformDirectoryPath dir = new PlatformDirectoryPath(PlatformFileType.Application, relativeFolder);
                PlatformFilePath pfp = new PlatformFilePath(dir, Path.GetFileName(fullPath));
                engineTex = TaleWorlds.Engine.Texture.CreateTextureFromPath(pfp);
                if (engineTex != null)
                {
                    Debug.Print("[DWDN] Loaded via CreateTextureFromPath: " + pfp);
                    return engineTex;
                }
            }
            catch (Exception ex)
            {
                Debug.Print("[DWDN] CreateTextureFromPath failed: " + ex.Message);
            }

            // Method 3: LoadTextureFromPath
            try
            {
                string folder = Path.GetDirectoryName(fullPath);
                string fileName = Path.GetFileName(fullPath);
                engineTex = TaleWorlds.Engine.Texture.LoadTextureFromPath(fileName, folder);
                if (engineTex != null)
                {
                    Debug.Print("[DWDN] Loaded via LoadTextureFromPath: " + fileName);
                    return engineTex;
                }
            }
            catch (Exception ex)
            {
                Debug.Print("[DWDN] LoadTextureFromPath failed: " + ex.Message);
            }

            return engineTex;
        }

        private static void UpdateSpriteGenericInPlace(SpriteData spriteData, string spriteName, SpritePart part)
        {
            if (spriteData == null || string.IsNullOrEmpty(spriteName) || part == null) return;
            try
            {
                if (spriteData.Sprites.TryGetValue(spriteName, out var existing) && existing is SpriteGeneric sg)
                {
                    var field = typeof(SpriteGeneric).GetField("_spritePart", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        field.SetValue(sg, part);
                        return;
                    }
                }
                spriteData.Sprites[spriteName] = new SpriteGeneric(spriteName, part, in SpriteNinePatchParameters.Empty);
            }
            catch
            {
                spriteData.Sprites[spriteName] = new SpriteGeneric(spriteName, part, in SpriteNinePatchParameters.Empty);
            }
        }

        private static void InjectIntoSpriteData(
            SpriteData spriteData,
            string categoryName,
            string primaryPartName,
            string secondaryPartName,
            TaleWorlds.TwoDimension.Texture twoDimTexture)
        {
            if (spriteData == null || twoDimTexture == null) return;

            try
            {
                int width = twoDimTexture.Width;
                int height = twoDimTexture.Height;

                SpriteCategory category;
                if (!spriteData.SpriteCategories.TryGetValue(categoryName, out category) || category == null)
                {
                    category = new SpriteCategory(categoryName, 1, true);
                    spriteData.SpriteCategories[categoryName] = category;
                }

                category.SpriteSheets.Clear();
                category.SpriteSheets.Add(twoDimTexture);
                category.SheetSizes = new Vec2i[] { new Vec2i(width, height) };

                // Set IsLoaded = true
                var isLoadedProp = typeof(SpriteCategory).GetProperty("IsLoaded");
                if (isLoadedProp != null && isLoadedProp.GetSetMethod(true) != null)
                {
                    isLoadedProp.GetSetMethod(true).Invoke(category, new object[] { true });
                }

                // Register primary sprite part and sprite
                if (!string.IsNullOrEmpty(primaryPartName))
                {
                    SpritePart part;
                    if (!spriteData.SpriteParts.TryGetValue(primaryPartName, out part) || part == null)
                    {
                        part = new SpritePart(primaryPartName, category, width, height)
                        {
                            SheetID = 1,
                            SheetX = 0,
                            SheetY = 0
                        };
                        spriteData.SpriteParts[primaryPartName] = part;
                    }
                    else
                    {
                        SetSpritePartCategory(part, category);
                        part.SheetID = 1;
                        part.SheetX = 0;
                        part.SheetY = 0;
                    }
                    part.UpdateInitValues();

                    UpdateSpriteGenericInPlace(spriteData, primaryPartName, part);
                }

                // Register secondary sprite part and sprite if provided
                if (!string.IsNullOrEmpty(secondaryPartName))
                {
                    SpritePart secPart;
                    if (!spriteData.SpriteParts.TryGetValue(secondaryPartName, out secPart) || secPart == null)
                    {
                        secPart = new SpritePart(secondaryPartName, category, width, height)
                        {
                            SheetID = 1,
                            SheetX = 0,
                            SheetY = 0
                        };
                        spriteData.SpriteParts[secondaryPartName] = secPart;
                    }
                    else
                    {
                        SetSpritePartCategory(secPart, category);
                        secPart.SheetID = 1;
                        secPart.SheetX = 0;
                        secPart.SheetY = 0;
                    }
                    secPart.UpdateInitValues();

                    UpdateSpriteGenericInPlace(spriteData, secondaryPartName, secPart);
                }
            }
            catch (Exception ex)
            {
                Debug.Print("[DWDN EXCEPTION] InjectIntoSpriteData failed: " + ex);
            }
        }

        private static void SetSpritePartCategory(SpritePart part, SpriteCategory category)
        {
            try
            {
                var field = typeof(SpritePart).GetField("_category", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(part, category);
                }
            }
            catch
            {
            }
        }

        private static bool IsIntermissionLayer(GauntletLayer gl)
        {
            if (gl == null) return false;

            if (gl.Name != null && gl.Name.IndexOf("Intermission", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (ScreenManager.TopScreen != null && ScreenManager.TopScreen.GetType().Name.IndexOf("Intermission", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (gl.UIContext?.Root != null)
            {
                if (HasIntermissionIndicators(gl.UIContext.Root))
                    return true;
            }

            return false;
        }

        private static bool HasIntermissionIndicators(Widget widget)
        {
            if (widget == null) return false;
            if (widget is TaleWorlds.MountAndBlade.GauntletUI.Widgets.Multiplayer.MultiplayerIntermissionNextMapImageWidget)
                return true;
            if (string.Equals(widget.Id, "LeaveButtonParent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(widget.Id, "AvailableMaps", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(widget.Id, "AvailableCultures", StringComparison.OrdinalIgnoreCase))
                return true;

            for (int i = 0; i < widget.ChildCount; i++)
            {
                if (HasIntermissionIndicators(widget.GetChild(i)))
                    return true;
            }
            return false;
        }

        private static void FixFactionBanners(Widget widget)
        {
            if (widget == null) return;

            if (widget is TaleWorlds.MountAndBlade.GauntletUI.Widgets.Multiplayer.MultiplayerFactionBannerWidget twBanner)
            {
                if (!string.IsNullOrEmpty(twBanner.FactionCode) && twBanner.FactionCode.IndexOf("dwdn", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (twBanner.Context?.SpriteData != null && _injectedFactionTexture != null)
                    {
                        var sprite = twBanner.Context.SpriteData.GetSprite(@"StdAssets\FactionIcons\LargeIcons\dwdn");
                        if (sprite == null || sprite.Texture == null || !sprite.Texture.IsValid)
                        {
                            InjectIntoSpriteData(twBanner.Context.SpriteData, "dwdn_faction", @"StdAssets\FactionIcons\LargeIcons\dwdn", @"StdAssets\FactionIcons\SmallIcons\dwdn", _injectedFactionTexture);
                        }
                    }

                    // Чтобы на стороне Защитников (Defender) гербовый щит не чернел, сбрасываем CultureColor2 и цвет виджета в белый
                    twBanner.CultureColor2 = Color.White;
                    if (twBanner.IconWidget != null)
                    {
                        if (twBanner.Context?.SpriteData != null)
                        {
                            var dwdnSprite = twBanner.Context.SpriteData.GetSprite(@"StdAssets\FactionIcons\LargeIcons\dwdn");
                            if (dwdnSprite != null && dwdnSprite.Texture != null && dwdnSprite.Texture.IsValid)
                            {
                                twBanner.IconWidget.Sprite = dwdnSprite;
                            }
                        }
                        twBanner.IconWidget.Color = Color.White;
                        twBanner.IconWidget.IsVisible = true;
                    }
                }
            }

            for (int i = 0; i < widget.ChildCount; i++)
            {
                FixFactionBanners(widget.GetChild(i));
            }
        }

        private static void CleanIntermissionWidgets(Widget widget)
        {
            if (widget == null) return;

            if (widget is TaleWorlds.MountAndBlade.GauntletUI.Widgets.Multiplayer.MultiplayerIntermissionNextMapImageWidget mapWidget)
            {
                mapWidget.IsVisible = false;
                mapWidget.Sprite = null;
            }
            else if (widget is TaleWorlds.MountAndBlade.GauntletUI.Widgets.ScreenBackgroundBrushWidget bgWidget)
            {
                if (bgWidget.Context?.SpriteData != null)
                {
                    var cleanBg = bgWidget.Context.SpriteData.GetSprite("dwdn_screen_load_1");
                    if (cleanBg != null && bgWidget.Sprite != cleanBg)
                    {
                        bgWidget.Sprite = cleanBg;
                    }
                }
            }
            else if (widget.Sprite != null && !string.IsNullOrEmpty(widget.Sprite.Name) &&
                     widget.Sprite.Name.IndexOf("dwdn", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     widget.Sprite.Name.IndexOf("screen_load", StringComparison.OrdinalIgnoreCase) < 0)
            {
                // Оставляем только легитимный значок внутри MultiplayerFactionBannerWidget (маленький герб в углу).
                // Любой другой элемент с гербом Daewden (растянутый фоновый щит по центру) скрываем!
                if (!(widget.ParentWidget is TaleWorlds.MountAndBlade.GauntletUI.Widgets.Multiplayer.MultiplayerFactionBannerWidget))
                {
                    widget.IsVisible = false;
                    widget.Sprite = null;
                }
            }

            for (int i = 0; i < widget.ChildCount; i++)
            {
                CleanIntermissionWidgets(widget.GetChild(i));
            }
        }

        private static void InjectIntoSpriteCategoryDirect(SpriteCategory cat)
        {
            if (cat == null || !IsTwoDimTextureAlive(_injectedLoadingTexture1)) return;
            try
            {
                var tex1 = _injectedLoadingTexture1;
                var tex2 = IsTwoDimTextureAlive(_injectedLoadingTexture2) ? _injectedLoadingTexture2 : tex1;

                int targetCount = Math.Max(12, Math.Max(cat.SpriteSheetCount, cat.SpriteSheets != null ? cat.SpriteSheets.Count : 0));

                // Если листы уже корректно заполнены нашими живыми обоями, не трогаем их повторно каждый кадр
                if (cat.SpriteSheets != null && cat.SpriteSheets.Count == targetCount && cat.SpriteSheets[0] == tex1)
                {
                    return;
                }

                cat.SpriteSheets.Clear();
                var sheetSizes = new Vec2i[targetCount];

                for (int i = 0; i < targetCount; i++)
                {
                    // Sheet 1 (i=0) и Sheet 2 (i=1) гарантированно tex1, Sheet 3 (i=2) — tex2
                    var t = (i == 0 || i == 1 || i % 2 == 1) ? tex1 : tex2;
                    cat.SpriteSheets.Add(t);
                    sheetSizes[i] = new Vec2i(t.Width, t.Height);
                }

                cat.SheetSizes = sheetSizes;
                cat.SpriteSheetCount = targetCount;

                var isLoadedProp = typeof(SpriteCategory).GetProperty("IsLoaded");
                if (isLoadedProp != null && isLoadedProp.GetSetMethod(true) != null)
                {
                    isLoadedProp.GetSetMethod(true).Invoke(cat, new object[] { true });
                }
            }
            catch (Exception ex)
            {
                Debug.Print("[DWDN EXCEPTION] InjectIntoSpriteCategoryDirect: " + ex);
            }
        }

        private static void InjectAllLoadingSprites(SpriteData spriteData)
        {
            if (spriteData == null || !IsTwoDimTextureAlive(_injectedLoadingTexture1)) return;

            try
            {
                var tex1 = _injectedLoadingTexture1;
                var tex2 = IsTwoDimTextureAlive(_injectedLoadingTexture2) ? _injectedLoadingTexture2 : tex1;

                int targetCount = 12;
                SpriteCategory category;
                if (!spriteData.SpriteCategories.TryGetValue("dwdn_loading", out category) || category == null)
                {
                    category = new SpriteCategory("dwdn_loading", targetCount, true);
                    spriteData.SpriteCategories["dwdn_loading"] = category;
                }

                category.SpriteSheets.Clear();
                var sheetSizes = new Vec2i[targetCount];
                for (int i = 0; i < targetCount; i++)
                {
                    // Sheet 1 (index 0) и Sheet 2 (index 1) -> tex1; Sheet 3 (index 2) -> tex2
                    var t = (i == 0 || i == 1 || i % 2 == 1) ? tex1 : tex2;
                    category.SpriteSheets.Add(t);
                    sheetSizes[i] = new Vec2i(t.Width, t.Height);
                }
                category.SheetSizes = sheetSizes;
                category.SpriteSheetCount = targetCount;

                var isLoadedProp = typeof(SpriteCategory).GetProperty("IsLoaded");
                if (isLoadedProp != null && isLoadedProp.GetSetMethod(true) != null)
                {
                    isLoadedProp.GetSetMethod(true).Invoke(category, new object[] { true });
                }

                if (spriteData.SpriteCategories.TryGetValue("ui_loading", out var uiLoadCat) && uiLoadCat != null)
                {
                    InjectIntoSpriteCategoryDirect(uiLoadCat);
                }
                // ui_mploading НЕ трогаем: в нем хранятся превью игровых карт (mp_battle_map и др.)

                SpritePart RegisterPartAndSprite(string name, int sheetId, TaleWorlds.TwoDimension.Texture tex)
                {
                    SpritePart part;
                    if (!spriteData.SpriteParts.TryGetValue(name, out part) || part == null)
                    {
                        part = new SpritePart(name, category, tex.Width, tex.Height)
                        {
                            SheetID = sheetId,
                            SheetX = 0,
                            SheetY = 0
                        };
                        spriteData.SpriteParts[name] = part;
                    }
                    else
                    {
                        SetSpritePartCategory(part, category);
                        part.SheetID = sheetId;
                        part.SheetX = 0;
                        part.SheetY = 0;
                    }
                    part.UpdateInitValues();
                    UpdateSpriteGenericInPlace(spriteData, name, part);
                    return part;
                }

                var part1 = RegisterPartAndSprite("dwdn_screen_load_1", 2, tex1);
                var part2 = RegisterPartAndSprite("dwdn_screen_load_2", 3, tex2);
                RegisterPartAndSprite("dwdn_screen_load", 2, tex1);
                RegisterPartAndSprite("dwdn_screen_load_3", 2, tex1);

                // Main menu & Intermission backgrounds (Standard.Background.Brush)
                UpdateSpriteGenericInPlace(spriteData, "background_1", part1);
                UpdateSpriteGenericInPlace(spriteData, "background_2", part1);
                UpdateSpriteGenericInPlace(spriteData, "background_3", part2);
                UpdateSpriteGenericInPlace(spriteData, "background_4", part1);

                // Loading screens 01..12 alternate between wallpaper 1 and 2
                for (int i = 1; i <= 12; i++)
                {
                    string loadName = "loading_" + (i < 10 ? "0" + i : i.ToString());
                    var p = (i % 2 == 1) ? part1 : part2;
                    UpdateSpriteGenericInPlace(spriteData, loadName, p);
                }
            }
            catch (Exception ex)
            {
                Debug.Print("[DWDN EXCEPTION] InjectAllLoadingSprites failed: " + ex);
            }
        }

        private static Sprite ResolveLoadingSprite(SpriteData spriteData, string name)
        {
            if (spriteData == null) return null;

            if (name == "dwdn_screen_load_2" || name == "loading_02" || name == "loading_04" || name == "loading_06" || name == "loading_08" || name == "loading_10" || name == "loading_12")
            {
                return spriteData.GetSprite("dwdn_screen_load_2") ?? spriteData.GetSprite("dwdn_screen_load_1");
            }
            return spriteData.GetSprite("dwdn_screen_load_1");
        }

        private static void FixLoadingWidgets(Widget widget)
        {
            if (widget == null) return;

            if (widget is TaleWorlds.MountAndBlade.GauntletUI.Widgets.Loading.LoadingWindowWidget lw)
            {
                if (lw.Context?.SpriteData != null && _injectedLoadingTexture1 != null)
                {
                    var targetSprite = ResolveLoadingSprite(lw.Context.SpriteData, lw.ImageName);
                    if (targetSprite != null)
                    {
                        if (lw.Sprite != targetSprite || lw.Sprite == null)
                        {
                            lw.Sprite = targetSprite;
                            lw.ImageName = targetSprite.Name;
                        }
                        if (lw.Color != Color.White)
                        {
                            lw.Color = Color.White;
                        }
                        if (lw.AlphaFactor != 1f)
                        {
                            lw.AlphaFactor = 1f;
                        }
                    }
                }
            }

            for (int i = 0; i < widget.ChildCount; i++)
            {
                FixLoadingWidgets(widget.GetChild(i));
            }
        }
#endif
    }
}
