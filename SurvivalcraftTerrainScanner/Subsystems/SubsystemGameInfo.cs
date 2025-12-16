using Game;
using TemplatesDatabase;

namespace SurvivalcraftTerrainScanner {
    public class SubsystemGameInfo : Game.SubsystemGameInfo {
        public override void Load(ValuesDictionary valuesDictionary) {
            WorldSettings = new WorldSettings();
            WorldSettings.Load(valuesDictionary);
            DirectoryName = valuesDictionary.GetValue<string>("WorldDirectoryName", null);
            WorldSeed = valuesDictionary.GetValue<int>("WorldSeed");
        }
    }
}