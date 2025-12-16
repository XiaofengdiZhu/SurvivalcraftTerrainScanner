using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Engine;
using Game;

namespace SurvivalcraftTerrainScanner {
    public static class GameInitializer {
        public static bool isInitialized;

        public static void Initialize() {
            if (isInitialized) {
                return;
            }
            //From Program
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            Log.RemoveAllLogSinks();
            Log.AddLogSink(new AnsiConsoleLogSink());
            Log.Information(
                $"Survivalcraft starting up at {DateTime.Now}, GameVersion={VersionsManager.Version}, BuildConfiguration={VersionsManager.BuildConfiguration}, Platform={VersionsManager.PlatformString}, Storage.AvailableFreeSpace={Storage.FreeSpace / 1024 / 1024}MB, ProcessorsCount={Environment.ProcessorCount}, APIVersion={ModsManager.APIVersionString}, 64bit={Environment.Is64BitProcess}"
            );
            try {
                SettingsManagerInitialize();
                Log.Information("Program Initialize Success");
            }
            catch (Exception e) {
                Log.Error(e.ToString());
            }
            //From LoadingScreen
            bool isLoadSucceed = true;
            ContentManager.Initialize();
            ModsManagerInitialize();
            ModsManager.ModList.Clear();
            foreach (ModEntity item in ModsManager.ModListAll) {
                if (item.IsDependencyChecked
                    || item.IsDisabled) {
                    continue;
                }
                item.CheckDependencies(ModsManager.ModList);
            }
            Dictionary<string, Assembly[]> assemblies = [];
            ModsManager.ModListAllDo(modEntity => {
                    bool flag = true;
                    assemblies[modEntity.modInfo.PackageName] = modEntity.GetAssemblies();
                    foreach (Assembly assembly in assemblies[modEntity.modInfo.PackageName]) {
                        if (flag) {
                            Log.Information($"[{modEntity.modInfo.Name}] Getting assemblies.");
                            flag = false;
                        }
                        AssemblyName assemblyName = assembly.GetName();
                        string fullName = assemblyName.FullName;
                        if (ModsManager.Dlls.TryGetValue(fullName, out Assembly existingAssembly)) {
                            if (existingAssembly.GetName().Version < assemblyName.Version) {
                                ModsManager.Dlls[fullName] = assembly;
                            }
                        }
                        else {
                            ModsManager.Dlls.Add(fullName, assembly);
                        }
                    }
                }
            );
            //加载 mod 程序集(.dll)文件
            //但不进行处理操作(如添加block等)
            ModsManager.ModListAllDo(modEntity => {
                    if (!isLoadSucceed) {
                        return;
                    }
                    foreach (Assembly asm in assemblies[modEntity.modInfo.PackageName]) {
                        Log.Information($"[{modEntity.modInfo.Name}] Handling assembly [{asm.FullName}]");
                        try {
                            modEntity.HandleAssembly(asm);
                        }
                        catch (Exception e) {
                            string separator = new('-', 10); //生成10个 '-' 连一起的字符串
                            Log.Error($"{separator}Handle assembly failed{separator}");
                            Log.Error(
                                $"Loaded assembly:\n{string.Join("\n", AppDomain.CurrentDomain.GetAssemblies().Select(x => x.FullName ?? x.GetName().FullName))}"
                            );
                            Log.Error(separator);
                            Log.Error($"Error assembly: {asm.FullName}");
                            Log.Error($"Dependencies:\n{string.Join("\n", asm.GetReferencedAssemblies().Select(x => x.FullName))}");
                            Log.Error(separator);
                            Log.Error(e);
                            isLoadSucceed = false;
                            break;
                        }
                    }
                }
            );
            if (!isLoadSucceed) {
                return;
            }
            LanguageControl.LanguageTypes.Clear();
            foreach (ContentInfo contentInfo in ContentManager.List("Lang")) {
                string fileName = Path.GetFileNameWithoutExtension(contentInfo.Filename);
                if (string.IsNullOrEmpty(fileName)) {
                    continue;
                }
                try {
                    CultureInfo cultureInfo = new(fileName.EndsWith("-old") ? fileName.Substring(0, fileName.Length - 4) : fileName, false);
                    LanguageControl.LanguageTypes.TryAdd(fileName, cultureInfo); //第二个参数应为CultureInfo
                }
                catch (Exception) {
                    // ignore
                }
            }
            //<<<结束
            if (ModsManager.Configs.TryGetValue("Language", out string value)
                && LanguageControl.LanguageTypes.ContainsKey(value)) {
                LanguageControl.Initialize(value);
            }
            else {
                bool languageNotLoaded = true;
                string systemLanguage = CultureInfo.CurrentUICulture.Name;
                if (string.IsNullOrEmpty(systemLanguage)) {
                    systemLanguage = RegionInfo.CurrentRegion.DisplayName != "United States" ? "zh-CN" : "en-US";
                }
                if (string.IsNullOrEmpty(systemLanguage)) {
                    //如果不支持系统语言，英语是最佳选择
                    LanguageControl.Initialize("en-US");
                    //languageNotLoaded = false;
                    Log.Information("Language is not specified, and system language is not detected, en-US is loaded instead.");
                }
                else if (LanguageControl.LanguageTypes.ContainsKey(systemLanguage)) {
                    LanguageControl.Initialize(systemLanguage);
                    //languageNotLoaded = false;
                    Log.Information($"Language is not specified, system language ({systemLanguage}) is successfully loaded.");
                }
                else {
                    CultureInfo systemCultureInfoParent = new CultureInfo(systemLanguage).Parent;
                    foreach ((string cultureName, CultureInfo cultureInfo) in LanguageControl.LanguageTypes) {
                        bool similar = false;
                        CultureInfo parentCulture = cultureInfo.Parent;
                        string parentCultureName = cultureInfo.Name;
                        if (parentCultureName == systemLanguage
                            || parentCultureName == systemCultureInfoParent.Name
                            || parentCultureName == systemCultureInfoParent.Parent.Name) {
                            similar = true;
                        }
                        else {
                            string rootCultureName = parentCulture.Parent.Name;
                            if (rootCultureName.Length > 0
                                && (rootCultureName == systemCultureInfoParent.Name || rootCultureName == systemCultureInfoParent.Parent.Name)) {
                                similar = true;
                            }
                        }
                        if (similar) {
                            LanguageControl.Initialize(cultureName);
                            Log.Information(
                                $"Language is not specified, a language ({cultureName}) closest to system language ({systemLanguage}) is successfully loaded."
                            );
                            languageNotLoaded = false;
                        }
                    }
                    if (languageNotLoaded) {
                        LanguageControl.Initialize("en-US");
                        Log.Information(
                            $"Language is not specified, and system language ({systemLanguage}) is not supported yet, en-US is loaded instead."
                        );
                    }
                }
            }
            ModsManager.ModListAllDo(modEntity => { modEntity.LoadLauguage(); });
            LanguageControl.SetUsual();
            DatabaseManager.Initialize();
            ModsManager.ModListAllDo(modEntity => { modEntity.LoadXdb(ref DatabaseManager.DatabaseNode); });
            DatabaseManager.LoadDataBaseFromXml(DatabaseManager.DatabaseNode);
            BlocksManagerInitialize();
            //CraftingRecipesManager.Initialize();
            WorldsManager.Initialize();
            isInitialized = true;
        }

        public static void SettingsManagerInitialize() {
            SettingsManager.DisplayLog = false;
            SettingsManager.DragHalfInSplit = true;
            SettingsManager.ShortInventoryLooping = false;
            SettingsManager.m_resolutionMode = ResolutionMode.High;
            SettingsManager.VisibilityRange = 128;
            SettingsManager.ViewAngle = 1f;
            SettingsManager.TerrainMipmapsEnabled = false;
            SettingsManager.SkyRenderingMode = SkyRenderingMode.Full;
            SettingsManager.ObjectsShadowsEnabled = true;
            SettingsManager.PresentationInterval = 1;
            SettingsManager.m_soundsVolume = 1.0f;
            SettingsManager.m_musicVolume = 0.2f;
            SettingsManager.m_brightness = 0.8f;
            SettingsManager.ShowGuiInScreenshots = false;
            SettingsManager.ShowLogoInScreenshots = true;
            //SettingsManager.ScreenshotSize = ScreenshotSize.ScreenSize;
            //SettingsManager.ScreenshotSizeCustomWidthIndex = 9;
            //SettingsManager.ScreenshotSizeCustomAspectRatioIndex = 5;
            SettingsManager.MoveControlMode = MoveControlMode.Buttons;
            SettingsManager.HideMoveLookPads = false;
            SettingsManager.HideCrosshair = false;
            SettingsManager.AllowInitialIntro = true;
            SettingsManager.DeleteWorldNeedToText = false;
            SettingsManager.BlocksTextureFileName = string.Empty;
            SettingsManager.LookControlMode = LookControlMode.EntireScreen;
            SettingsManager.FlipVerticalAxis = false;
            SettingsManager.UIScale = 0.75f;
            SettingsManager.AutoJump = false;
            SettingsManager.MoveSensitivity = 0.5f;
            SettingsManager.LookSensitivity = 0.5f;
            SettingsManager.GamepadDeadZone = 0.16f;
            SettingsManager.GamepadCursorSpeed = 1f;
            SettingsManager.GamepadTriggerThreshold = 0.5f;
            SettingsManager.CreativeDigTime = 0.33f;
            SettingsManager.CreativeReach = 7.5f;
            SettingsManager.MinimumHoldDuration = 0.25f;
            SettingsManager.MinimumDragDistance = 10f;
            SettingsManager.HorizontalCreativeFlight = false;
            SettingsManager.DropboxAccessToken = string.Empty;
            SettingsManager.ScpboxAccessToken = string.Empty;
            SettingsManager.MotdUpdateUrl = "https://m.schub.top/com/motd?v={0}&l={1}";
            SettingsManager.MotdUpdateCheckUrl = "https://m.schub.top/com/motd?v={0}&cmd=version_check&platform={1}&apiv={2}&l={3}";
            SettingsManager.MotdUpdatePeriodHours = 12.0;
            SettingsManager.MotdLastUpdateTime = DateTime.MinValue;
            SettingsManager.MotdLastDownloadedData = string.Empty;
            SettingsManager.UserId = string.Empty;
            SettingsManager.LastLaunchedVersion = string.Empty;
            SettingsManager.CommunityContentMode = CommunityContentMode.Normal;
            SettingsManager.OriginalCommunityContentMode = CommunityContentMode.Normal;
            SettingsManager.MultithreadedTerrainUpdate = true;
            SettingsManager.NewYearCelebrationLastYear = 2025;
            SettingsManager.ScreenLayout1 = ScreenLayout.Single;
            SettingsManager.ScreenLayout2 = ScreenLayout.DoubleVertical;
            SettingsManager.ScreenLayout3 = ScreenLayout.TripleVertical;
            SettingsManager.ScreenLayout4 = ScreenLayout.Quadruple;
            SettingsManager.BulletinTime = string.Empty;
            SettingsManager.ScpboxUserInfo = string.Empty;
            SettingsManager.HorizontalCreativeFlight = true;
            SettingsManager.CreativeDragMaxStacking = true;
            SettingsManager.LowFPSToTimeDeceleration = 10;
            SettingsManager.UseAPISleepTimeAcceleration = false;
            //MoveWidgetSize = 1f;
            SettingsManager.MoveWidgetMarginX = 0f;
            SettingsManager.MoveWidgetMarginY = 0f;
            SettingsManager.AnimatedTextureRefreshLimit = 7;
            SettingsManager.FileAssociationEnabled = true;
            SettingsManager.SafeMode = false;
            //SettingsManager.InitializeKeyboardMappingSettings();
            //SettingsManager.InitializeGamepadMappingSettings();
            //SettingsManager.InitializeCameraManageSettings();
            SettingsManager.LoadSettings();
        }

        public static void ModsManagerInitialize() {
            ModsManager.ModListAll.Add(new SurvivalCraftModEntity());
        }

        public static void BlocksManagerInitialize() {
            BlocksManager.LoadBlocksStaticly = true;
            BlocksManager.InitializeCategories();
            BlocksManager.CalculateSlotTexCoordTables();
            BlocksManager.InitializeBlocks(null);
            BlocksManager.ResetBlocks();
            foreach (ModEntity modEntity in ModsManager.ModList) {
                modEntity.LoadBlocksData();
            }
            foreach (Block block in BlocksManager.m_blocks) {
                /*try {
                    block.Initialize();
                }
                catch (Exception e) {
                    Log.Warning($"Loading Block {block.GetType().Name} error.\n{e.Message}");
                }*/
                foreach (int value in block.GetCreativeValues()) {
                    string category = block.GetCategory(value);
                    BlocksManager.AddCategory(category);
                }
            }
        }
    }
}