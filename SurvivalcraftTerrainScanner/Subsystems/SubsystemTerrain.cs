using Game;
using TemplatesDatabase;

namespace SurvivalcraftTerrainScanner {
    public class SubsystemTerrain : Game.SubsystemTerrain {
        public override void Load(ValuesDictionary valuesDictionary) {
            SubsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
            Terrain = new Terrain();
            //TerrainUpdater = new TerrainUpdater(this);
            if (!string.IsNullOrEmpty(SubsystemGameInfo.DirectoryName)) {
                TerrainSerializer = new TerrainSerializer23(SubsystemGameInfo.DirectoryName);
            }
            TerrainGenerationMode terrainGenerationMode = SubsystemGameInfo.WorldSettings.TerrainGenerationMode;
            if (string.CompareOrdinal(SubsystemGameInfo.WorldSettings.OriginalSerializationVersion, "2.1") <= 0) {
                if (terrainGenerationMode == TerrainGenerationMode.FlatContinent
                    || terrainGenerationMode == TerrainGenerationMode.FlatIsland) {
                    TerrainContentsGenerator = new TerrainContentsGeneratorFlat(this);
                }
                else {
                    TerrainContentsGenerator = new TerrainContentsGenerator21(this);
                }
            }
            else if (string.CompareOrdinal(SubsystemGameInfo.WorldSettings.OriginalSerializationVersion, "2.2") == 0) {
                if (terrainGenerationMode == TerrainGenerationMode.FlatContinent
                    || terrainGenerationMode == TerrainGenerationMode.FlatIsland) {
                    TerrainContentsGenerator = new TerrainContentsGeneratorFlat(this);
                }
                else {
                    TerrainContentsGenerator = new TerrainContentsGenerator22(this);
                }
            }
            else if (string.CompareOrdinal(SubsystemGameInfo.WorldSettings.OriginalSerializationVersion, "2.3") == 0) {
                if (terrainGenerationMode == TerrainGenerationMode.FlatContinent
                    || terrainGenerationMode == TerrainGenerationMode.FlatIsland) {
                    TerrainContentsGenerator = new TerrainContentsGeneratorFlat(this);
                }
                else {
                    TerrainContentsGenerator = new TerrainContentsGenerator23(this);
                }
            }
            else if (terrainGenerationMode == TerrainGenerationMode.FlatContinent
                || terrainGenerationMode == TerrainGenerationMode.FlatIsland) {
                TerrainContentsGenerator = new TerrainContentsGeneratorFlat(this);
            }
            else {
                TerrainContentsGenerator = new TerrainContentsGenerator24(this);
            }
        }

        public override void Dispose() {
            TerrainSerializer?.Dispose();
            Terrain.Dispose();
        }
    }
}