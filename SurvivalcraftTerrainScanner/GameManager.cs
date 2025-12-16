using System;
using System.IO;
using System.Xml.Linq;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;
using XmlUtilities;

namespace SurvivalcraftTerrainScanner {
    public static class GameManager {
        public static WorldInfo m_worldInfo;
        public static Project m_project;
        public static Project Project => m_project;
        public const string fName = "GameManager";

        public static void LoadProject(WorldInfo worldInfo) {
            DisposeProject();
            WorldsManager.RepairWorldIfNeeded(worldInfo.DirectoryName);
            VersionsManager.UpgradeWorld(worldInfo.DirectoryName);
            BlocksManager.LoadBlocksStaticly = string.IsNullOrEmpty(worldInfo.APIVersion);
            using (Stream stream = Storage.OpenFile(Storage.CombinePaths(worldInfo.DirectoryName, "Project.xml"), OpenFileMode.Read)) {
                ValuesDictionary valuesDictionary = new();
                ValuesDictionary valuesDictionary2 = new();
                valuesDictionary.SetValue("GameInfo", valuesDictionary2);
                valuesDictionary2.SetValue("WorldDirectoryName", worldInfo.DirectoryName);
                XElement projectNode = XmlUtils.LoadXmlFromStream(stream, null, true);
                ProjectData projectData = new(DatabaseManager.GameDatabase, projectNode, valuesDictionary, true);
                m_project = new Project(DatabaseManager.GameDatabase, projectData);
            }
            m_worldInfo = worldInfo;
            Log.Information(
                LanguageControl.Get(fName, "1"),
                LanguageControl.Get("GameMode", worldInfo.WorldSettings.GameMode.ToString()),
                LanguageControl.Get("StartingPositionMode", worldInfo.WorldSettings.StartingPositionMode.ToString()),
                worldInfo.WorldSettings.Name,
                SettingsManager.VisibilityRange.ToString(),
                LanguageControl.Get("ResolutionMode", SettingsManager.ResolutionMode.ToString())
            );
            GC.Collect();
        }

        public static void LoadVirtualProject(WorldInfo worldInfo) {
            DisposeProject();
            WorldSettings worldSettings = worldInfo.WorldSettings;
            /*if (!WorldsManager.ValidateWorldName(worldSettings.Name)) {
                throw new InvalidOperationException($"World name \"{worldSettings.Name}\" is invalid.");
            }*/
            /*int num1;
            if (worldSettings.CustomWorldSeed) {
                num1 = worldSettings.WorldSeed;
            }
            else if (string.IsNullOrEmpty(worldSettings.Seed)) {
                num1 = (int)(long)(Time.RealTime * 1000.0);
            }
            else if (worldSettings.Seed == "0") {
                num1 = 0;
            }
            else {
                num1 = 0;
                int num2 = 1;
                foreach (char ch in worldSettings.Seed) {
                    num1 += ch * num2;
                    num2 += 29;
                }
            }*/
            ValuesDictionary valuesDictionary1 = new();
            worldSettings.Save(valuesDictionary1, false);
            //valuesDictionary1.SetValue<string>("WorldDirectoryName", worldDirectoryName);
            //valuesDictionary1.SetValue("WorldSeed", num1);
            valuesDictionary1.SetValue("WorldSeed", 0);
            ValuesDictionary valuesDictionary2 = new();
            valuesDictionary2.SetValue("Players", new ValuesDictionary());
            DatabaseObject databaseObject = DatabaseManager.GameDatabase.Database.FindDatabaseObject(
                "GameProject",
                DatabaseManager.GameDatabase.ProjectTemplateType,
                true
            );
            XElement node = new((XName)"Project");
            XmlUtils.SetAttributeValue(node, "Guid", databaseObject.Guid);
            XmlUtils.SetAttributeValue(node, "Name", "GameProject");
            XmlUtils.SetAttributeValue(node, "Version", VersionsManager.SerializationVersion);
            XmlUtils.SetAttributeValue(node, "APIVersion", ModsManager.APIVersionString);
            XElement content = new((XName)"Subsystems");
            node.Add(content);
            XElement xelement1 = new((XName)"Values");
            XmlUtils.SetAttributeValue(xelement1, "Name", "GameInfo");
            valuesDictionary1.Save(xelement1);
            content.Add(xelement1);
            XElement xelement2 = new((XName)"Values");
            XmlUtils.SetAttributeValue(xelement2, "Name", "Players");
            valuesDictionary2.Save(xelement2);
            content.Add(xelement2);
            ValuesDictionary valuesDictionary3 = new();
            ValuesDictionary valuesDictionary4 = new();
            valuesDictionary3.SetValue("GameInfo", valuesDictionary4);
            //valuesDictionary4.SetValue("WorldDirectoryName", worldInfo.DirectoryName);
            ProjectData projectData = new(DatabaseManager.GameDatabase, node, valuesDictionary3, true);
            m_project = new Project(DatabaseManager.GameDatabase, projectData);
            m_worldInfo = worldInfo;
            GC.Collect();
        }

        public static void DisposeProject() {
            if (m_project != null) {
                m_project.Dispose();
                m_project = null;
                m_worldInfo = null;
                GC.Collect();
            }
        }
    }
}