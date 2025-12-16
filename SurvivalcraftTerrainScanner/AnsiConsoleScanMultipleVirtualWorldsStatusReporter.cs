//Obsolete

namespace SurvivalcraftTerrainScanner {
    public class AnsiConsoleScanMultipleVirtualWorldsStatusReporter : AnsiConsoleStatusReporter {
        public string Stage {
            get;
            set {
                field = value;
                ReportStatus($"{Stage}");
            }
        }

        public string Progress {
            get;
            set {
                field = value;
                ReportStatus($"{Stage} - {value}");
            }
        }
    }
}