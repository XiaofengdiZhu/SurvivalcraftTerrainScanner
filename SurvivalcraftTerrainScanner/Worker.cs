using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;
using XmlUtilities;

namespace SurvivalcraftTerrainScanner {
    public class ScanResult {
        public int Seed;
        public int TotalBlocksCount;
        public Dictionary<int, int> BlocksCount;

        public ScanResult(int seed, int totalBlocksCount, Dictionary<int, int> blocksCount) {
            Seed = seed;
            TotalBlocksCount = totalBlocksCount;
            BlocksCount = blocksCount;
        }
    }

    public class Worker : IDisposable {
        public readonly int m_scanRange;
        public readonly HashSet<int> m_targets;
        public readonly int m_minTargetBlockSum;
        public Project m_project;
        public ITerrainContentsGenerator m_terrainGenerator;
        public Terrain m_terrain;
        public TerrainUpdater m_terrainUpdater;
        public TerrainScanner m_terrainScanner;

        public Worker(WorldSettings worldSettings, int scanRange, HashSet<int> targets, int minTargetBlockSum) {
            m_scanRange = scanRange;
            m_targets = targets;
            m_minTargetBlockSum = minTargetBlockSum;
            LoadVirtualProject(new WorldInfo { SerializationVersion = VersionsManager.SerializationVersion, WorldSettings = worldSettings });
            SubsystemTerrain subsystemTerrain = m_project.FindSubsystem<SubsystemTerrain>(true);
            m_terrainGenerator = subsystemTerrain.TerrainContentsGenerator;
            m_terrain = subsystemTerrain.Terrain;
            m_terrainUpdater = new TerrainUpdater(subsystemTerrain);
            m_terrainScanner = new TerrainScanner(subsystemTerrain);
        }

        public ScanResult Scan(int seed) {
            if (m_terrainGenerator is TerrainContentsGenerator24 generator24) {
                generator24.m_seed = seed;
                Game.Random random = new(seed);
                generator24.m_temperatureOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
                generator24.m_humidityOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
                generator24.m_mountainsOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
                generator24.m_riversOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
            }
            else if (m_terrainGenerator is TerrainContentsGeneratorFlat generatorFlat) {
                Game.Random random = new(seed);
                generatorFlat.m_shoreRoughnessOffset[0] = random.Float(-2000f, 2000f);
                generatorFlat.m_shoreRoughnessOffset[1] = random.Float(-2000f, 2000f);
                generatorFlat.m_shoreRoughnessOffset[2] = random.Float(-2000f, 2000f);
                generatorFlat.m_shoreRoughnessOffset[3] = random.Float(-2000f, 2000f);
            }
            m_terrain.m_allocatedChunks.Clear();
            m_terrain.m_allocatedChunksArray = null;
            foreach (TerrainChunk chunk in m_terrain.m_allChunks.m_array) {
                chunk?.Dispose();
            }
            Array.Fill(m_terrain.m_allChunks.m_array, null);
            Vector3 spawnPosition = m_terrainGenerator.FindCoarseSpawnPosition();
            m_terrainUpdater.SpawnPosition = spawnPosition.XZ;
            m_terrainUpdater.GenerateChunks(m_scanRange);
            Point3 spawnPositionPoint3 = new(spawnPosition);
            spawnPositionPoint3.Y = m_terrainScanner.FindTopmostHeight(spawnPositionPoint3.X, spawnPositionPoint3.Z);
            Dictionary<int, int> blocksCount = [];
            int totalBlocksCount = 0;
            m_terrainScanner.DigBlocks(
                spawnPositionPoint3,
                m_targets,
                (block, position) => {
                    if (blocksCount.TryGetValue(block, out int count)) {
                        blocksCount[block] = count + 1;
                    }
                    else {
                        blocksCount.Add(block, 1);
                    }
                    totalBlocksCount++;
                }
            );
            return totalBlocksCount >= m_minTargetBlockSum ? new ScanResult(seed, totalBlocksCount, blocksCount) : null;
        }

        public void LoadVirtualProject(WorldInfo worldInfo) {
            DisposeProject();
            WorldSettings worldSettings = worldInfo.WorldSettings;
            /*if (!WorldsManager.ValidateWorldName(worldSettings.Name)) {
                throw new InvalidOperationException($"World name \"{worldSettings.Name}\" is invalid.");
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
            GC.Collect();
        }

        public void DisposeProject() {
            if (m_project != null) {
                m_project.Dispose();
                m_project = null;
            }
        }

        public void Dispose() {
            m_terrainGenerator = null;
            m_terrain = null;
            m_terrainUpdater = null;
            m_terrainScanner = null;
            DisposeProject();
        }
    }
}