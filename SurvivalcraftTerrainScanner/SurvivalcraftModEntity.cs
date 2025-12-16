using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Engine;
using Game;
using Game.IContentReader;

namespace SurvivalcraftTerrainScanner {
    public class SurvivalCraftModEntity : Game.SurvivalCraftModEntity {
        public override void InitResources() {
            ModFiles.Clear();
            if (ModArchive == null) {
                return;
            }
            List<ZipArchiveEntry> entries = ModArchive.ReadCentralDir();
            foreach (ZipArchiveEntry zipArchiveEntry in entries) {
                if (zipArchiveEntry.FileSize > 0) {
                    ModFiles.Add(zipArchiveEntry.FilenameInZip, zipArchiveEntry);
                }
            }
            /*if (GetFile("icon.webp", LoadIcon)) {
                GetFile("icon.png", LoadIcon);
            }*/
            GetFile("modinfo.json", stream => { modInfo = ModsManager.DeserializeJson(ModsManager.StreamToString(stream)); });
            if (modInfo == null) {
                IsDisabled = true;
                DisableReason = ModDisableReason.NoModInfo;
                return;
            }
            if (modInfo.PackageName.Contains(';')) {
                IsDisabled = true;
                DisableReason = ModDisableReason.InvalidPackageName;
                return;
            }
            if (ModsManager.DisabledMods.TryGetValue(modInfo.PackageName, out HashSet<string> disabledVersions)
                && disabledVersions.Contains(modInfo.Version)) {
                IsDisabled = true;
                DisableReason = ModDisableReason.Manually;
                return;
            }
            foreach (KeyValuePair<string, ZipArchiveEntry> c in ModFiles) {
                ZipArchiveEntry zipArchiveEntry = c.Value;
                string filename = zipArchiveEntry.FilenameInZip;
                if (!zipArchiveEntry.IsFilenameUtf8) {
                    ModsManager.AddException(
                        new Exception(
                            $"[{modInfo.Name}] The file name [{zipArchiveEntry.FilenameInZip}] is not encoded in UTF-8, need to be corrected."
                        )
                    );
                }
                if (filename.StartsWith("Assets/")) {
                    MemoryStream memoryStream = new();
                    ContentInfo contentInfo = new(filename.Substring(7));
                    ModArchive.ExtractFile(zipArchiveEntry, memoryStream);
                    contentInfo.SetContentStream(memoryStream);
                    ContentManager.Add(contentInfo);
                }
            }
            Log.Information($"[{modInfo.Name}] Loaded {ModFiles.Count} resource files.");
        }

        public override void HandleAssembly(Assembly assembly) {
            Type[] types = assembly.GetTypes();
            foreach (Type type in types) {
                /*if (type.IsSubclassOf(typeof(ModLoader))
                    && !type.IsAbstract) {
                    if (Activator.CreateInstance(type) is not ModLoader modLoader) {
                        continue;
                    }
                    modLoader.Entity = this;
                    modLoader.__ModInitialize();
                    Loader = modLoader;
                    ModsManager.ModLoaders.Add(modLoader);
                }
                else */if (type.IsSubclassOf(typeof(Block))
                    && !type.IsAbstract) {
                    FieldInfo fieldInfo = type.GetRuntimeFields().FirstOrDefault(p => p.Name == "Index" && p.IsPublic && p.IsStatic);
                    if (fieldInfo == null
                        || fieldInfo.FieldType != typeof(int)) {
                        ModsManager.AddException(
                            new InvalidOperationException($"Block type \"{type.FullName}\" does not have static field Index of type int.")
                        );
                    }
                    else {
                        /*var index = (int)fieldInfo.GetValue(null);
                        var block = (Block)Activator.CreateInstance(type.GetTypeInfo().AsType());
                        block.BlockIndex = index;*/
                        BlockTypes.Add(type);
                    }
                }
            }
        }
        public override void LoadLauguage() {
            GetAssetsFile(
                "Lang/en-US.json",
                stream => {
                    Log.Information($"[{modInfo.Name}] Loading English Language file");
                    LanguageControl.LoadEnglishJson(stream);
                }
            );
            string language = ModsManager.Configs["Language"];
            if (language == "en-US") {
                return;
            }
            GetAssetsFile(
                $"Lang/{language}.json",
                stream => {
                    Log.Information($"[{modInfo.Name}] Loading Current Language file");
                    LanguageControl.loadJson(stream);
                }
            );
        }

        public override void LoadXdb(ref XElement xElement) {
            Log.Information($"[{modInfo?.Name}] {LanguageControl.Get(fName, "2")}");
            xElement = ContentManager.Get<XElement>("Database");
            ContentManager.Dispose("Database");
        }

        public override void LoadBlocksData() {
            Log.Information($"[{modInfo?.Name}] {LanguageControl.Get(fName, "1")}");
            BlocksManager.LoadBlocksData(ContentManager.Get<string>("BlocksData"));
            ContentManager.Dispose("BlocksData");
        }
    }
}