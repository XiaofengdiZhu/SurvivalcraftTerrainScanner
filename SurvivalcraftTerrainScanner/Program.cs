using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Engine;
using Game;
using GameEntitySystem;
using Spectre.Console;

namespace SurvivalcraftTerrainScanner {
    public class Program {
        public static Game.Random m_random = new();

        public static void Main(string[] args) {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.White;
            if (OperatingSystem.IsWindows()) {
                try {
                    if (Console.WindowHeight < 120) {
                        Console.WindowWidth = 120;
                    }
                }
                catch {
                    Console.WriteLine("终端窗口宽度调整失败，可能导致意外的换行");
                }
            }
            else if (Console.WindowHeight < 120) {
                Console.WriteLine("终端窗口宽度过短，可能导致意外的换行");
            }
            AnsiConsole.Foreground = Spectre.Console.Color.White;
            AnsiConsole.MarkupLine("[yellow]欢迎使用生存战争地形扫描器[/]");
            AnsiConsoleStatusReporter gameInitializingStatusReporter = new();
            _ = gameInitializingStatusReporter.StartAsync("正在初始化游戏");
            Stopwatch sw = Stopwatch.StartNew();
            try {
                GameInitializer.Initialize();
            }
            catch (Exception e) {
                sw.Stop();
                AnsiConsole.MarkupLine("游戏初始化失败");
                AnsiConsole.WriteLine(e.ToString());
                Exit();
                return;
            }
            sw.Stop();
            AnsiConsole.MarkupLine($"游戏初始化完成，耗时：[green]{sw.ElapsedMilliseconds}ms[/]");
            _ = gameInitializingStatusReporter.CompleteAsync();
            AnsiConsole.WriteLine();
            SelectionPrompt<int> selectionPrompt = new SelectionPrompt<int>().Title("请选择要执行的操作：[grey50]（按上/下键来选择）[/]")
                .MoreChoicesText("[grey50]（上/下还有更多）[/]");
            selectionPrompt.Converter = i => i switch {
                0 => "创建/选择世界并扫描",
                1 => "多线程扫描指定范围内的真种子创建的世界",
                _ => "退出"
            };
            selectionPrompt.AddChoices(0, 1, 2);
            int selection = AnsiConsole.Prompt(selectionPrompt);
            AnsiConsole.MarkupLine("请选择要执行的操作：[grey50]（按上/下键来选择）[/]");
            AnsiConsole.MarkupLine($"[blue]> {selectionPrompt.Converter(selection)}[/]");
            switch (selection) {
                case 0:
                    ChooseAndScanWorld();
                    Exit();
                    break;
                case 1:
                    ScanMultipleVirtualWorlds();
                    Exit();
                    break;
            }
        }

        static void ChooseAndScanWorld() {
            ConfirmationPrompt selectAnotherWorldPrompt = new("是否选择其他世界？") { DefaultValue = false };
            while (true) {
                WorldInfo worldInfo = ChooseWorld();
                if (string.IsNullOrEmpty(worldInfo.DirectoryName)) {
                    GameManager.LoadVirtualProject(worldInfo);
                }
                else {
                    GameManager.LoadProject(worldInfo);
                }
                Project project = GameManager.Project;
                SubsystemTerrain subsystemTerrain = project.FindSubsystem<SubsystemTerrain>(true);
                Vector3 spawnPosition = subsystemTerrain.TerrainContentsGenerator.FindCoarseSpawnPosition();
                AnsiConsole.MarkupLine($"出生点：[darkolivegreen3_1]({MathF.Round(spawnPosition.X, 2)}, {MathF.Round(spawnPosition.Z, 2)}[/])");
                TerrainUpdater terrainUpdater = new(subsystemTerrain) { SpawnPosition = spawnPosition.XZ };
                int scanRange = AnsiConsole.Prompt(
                    new TextPrompt<int>("请输入以此为中心的扫描范围：[grey50]（>= 1）[/]")
                        .Validate(num => num >= 1 ? ValidationResult.Success() : ValidationResult.Error("请输入有效的数字"))
                        .DefaultValue(100)
                );
                AnsiConsoleProgressBarReporter generateChunksReporter = new();
                _ = generateChunksReporter.StartAsync(["分配区块", "生成/加载区块"]);
                terrainUpdater.GenerateChunks(scanRange, generateChunksReporter);
                _ = generateChunksReporter.CompleteAsync();
                AnsiConsole.WriteLine("地形生成完成");
                TerrainScanner terrainScanner = new(subsystemTerrain);
                Point3 spawnPositionPoint3 = new(spawnPosition);
                spawnPositionPoint3.Y = terrainScanner.FindTopmostHeight(spawnPositionPoint3.X, spawnPositionPoint3.Z);
                AnsiConsole.MarkupLine($"最终出生点：[darkolivegreen3_1]({spawnPositionPoint3})[/]，将从此开始扫描");
                List<(string CategoryName, List<(string DisplayName, HashSet<int> BlockValues)> CategoryItems)> targetsCategorized = [
                    ("矿物",
                    [
                        ("煤矿", [CoalOreBlock.Index]),
                        ("铜矿", [CopperOreBlock.Index]),
                        ("铁矿", [IronOreBlock.Index]),
                        ("硫磺矿", [SulphurOreBlock.Index]),
                        ("钻石矿", [DiamondOreBlock.Index]),
                        ("锗矿", [GermaniumOreBlock.Index]),
                        ("硝石矿", [SaltpeterOreBlock.Index])
                    ]),
                    ("植物",
                    [
                        ("南瓜", [PumpkinBlock.Index]),
                        ("棉花", [Terrain.MakeBlockValue(CottonBlock.Index, 0, CottonBlock.SetSize(CottonBlock.SetIsWild(0, true), 2))])
                    ])
                ];
                /*HashSet<int> ryeValues = [];
                for (int i = 3; i < 8; i++) {
                    ryeValues.Add(Terrain.MakeBlockValue(RyeBlock.Index, 0, RyeBlock.SetSize(RyeBlock.SetIsWild(0, true), i)));
                }
                targetsCategorized[1].CategoryItems.Insert(0, ("黑麦", ryeValues));*/
                HashSet<int> gravestoneValues = [CairnBlock.Index];
                for (int i = 0; i < 16; i++) {
                    gravestoneValues.Add(Terrain.MakeBlockValue(GravestoneBlock.Index, 0, i));
                }
                targetsCategorized.Add(("物品", [("墓碑", gravestoneValues)]));
                HashSet<int> targetsHashSet = targetsCategorized.SelectMany(category => category.CategoryItems.SelectMany(item => item.BlockValues))
                    .ToHashSet();
                Dictionary<int, int> blocksCount = [];
                int totalBlocksCount = 0;
                Stopwatch sw = Stopwatch.StartNew();
                terrainScanner.DigBlocks(
                    spawnPositionPoint3,
                    targetsHashSet,
                    (block, _) => {
                        if (blocksCount.TryGetValue(block, out int count)) {
                            blocksCount[block] = count + 1;
                        }
                        else {
                            blocksCount.Add(block, 1);
                        }
                        totalBlocksCount++;
                    }
                );
                sw.Stop();
                GameManager.DisposeProject();
                if (totalBlocksCount > 0) {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"地形扫描完成，耗时：[green]{sw.Elapsed.TotalSeconds:F3}s[/]");
                    AnsiConsole.MarkupLine(
                        $"| 世界真种子 | 总数 |[grey82]{"矿物".PadCenterMono(54)}[/]|[#40a040]{"植物".PadCenterMono(13)}[/]| 物品 |".Replace("|", "[grey50]|[/]")
                    );
                    AnsiConsole.MarkupLine(
                        "|            |      |[#1F1F1F on grey82] 煤矿 [/]|[#26846D on grey82] 铜矿 [/]|[#87562F on grey70] 铁矿 [/]|[#89843F on grey70] 硫磺矿 [/]|[#0D6D99 on grey70] 钻石矿 [/]|[#6F694F on grey70] 锗矿 [/]|[#FEFEFE on #C2B283] 硝石矿 [/]| [darkorange]南瓜[/] | [#F1F0EA]棉花[/] | [grey82]墓碑[/] |"
                            .Replace("|", "[grey50]|[/]")
                    );
                    StringBuilder sb = new(120);
                    sb.Append($"|{worldInfo.WorldSettings.WorldSeed.ToString().PadLeft(11)} | [yellow]{totalBlocksCount.ToString().PadLeft(4)}[/] |");
                    foreach ((string _, List<(string DisplayName, HashSet<int> BlockValues)> categoryItems) in targetsCategorized) {
                        foreach ((string displayName, HashSet<int> blockValues) in categoryItems) {
                            int sum = 0;
                            foreach (int value in blockValues) {
                                if (blocksCount.TryGetValue(value, out int count)) {
                                    sum += count;
                                }
                            }
                            sb.Append($" {(sum == 0 ? "[grey58]" : "[darkolivegreen3_1]")}{sum.ToString().PadLeft(displayName.Length * 2)}[/] |");
                        }
                    }
                    AnsiConsole.MarkupLine(sb.Replace("|", "[grey50]|[/]").ToString());
                }
                else {
                    AnsiConsole.MarkupLine("地形扫描完成，但结果为空");
                }
                if (!AnsiConsole.Prompt(selectAnotherWorldPrompt)) {
                    break;
                }
            }
        }

        static WorldInfo ChooseWorld() {
            WorldsManager.UpdateWorldsList();
            if (WorldsManager.WorldInfos.Count == 0) {
                if (AnsiConsole.Prompt(new ConfirmationPrompt("未找到世界，是否创建？"))) {
                    AnsiConsole.WriteLine();
                    return CreateWorld();
                }
                Exit();
            }
            SelectionPrompt<WorldInfo> selectWorldPrompt = new SelectionPrompt<WorldInfo>().Title("请选择世界：[grey50]（按上/下键来选择）[/]")
                .MoreChoicesText("[grey]上/下还有更多[/]")
                .AddChoices(WorldsManager.WorldInfos);
            selectWorldPrompt.AddChoice(null);
            selectWorldPrompt.Converter = info => info?.WorldSettings.Name ?? "[green4]或者创建新的世界[/]";
            WorldInfo selectWorld = AnsiConsole.Prompt(selectWorldPrompt);
            if (selectWorld == null) {
                AnsiConsole.MarkupLine("请选择世界：[grey50]（按上/下键来选择）[/]");
                AnsiConsole.MarkupLine("[blue]> 或者创建新的世界[/]");
                return CreateWorld();
            }
            AnsiConsole.MarkupLine("请选择世界：[grey50]（按上/下键来选择）[/]");
            AnsiConsole.MarkupLine($"[blue]> {selectWorld.WorldSettings.Name}[/]");
            return selectWorld;
        }

        static WorldInfo CreateWorld() {
            WorldSettings worldSettings = CreateWorldSettings();
            AnsiConsole.MarkupLine("[yellow]开始创建世界！[/]");
            return WorldsManager.CreateWorld(worldSettings);
        }

        static void ScanMultipleVirtualWorlds() {
            int startSeed = AnsiConsole.Prompt(
                new TextPrompt<int>("请真种子起点：[grey50]（>= 32, <= 173864355）[/]")
                    .Validate(count => count is >= 32 and <= 173864355 ? ValidationResult.Success() : ValidationResult.Error("数量不合法"))
                    .DefaultValue(32)
            );
            int worldCount = AnsiConsole.Prompt(
                new TextPrompt<int>($"请输入要生成并扫描的世界数量：[grey50]（>= 1, <= {173864355 - startSeed + 1}）[/]")
                    .Validate(count => count >= 1 && count <= 173864355 - startSeed + 1 ? ValidationResult.Success() : ValidationResult.Error("数量不合法")
                    )
                    .DefaultValue(100000)
            );
            int scanRange = AnsiConsole.Prompt(
                new TextPrompt<int>("请输入扫描范围：[grey50]（>= 1）[/]")
                    .Validate(num => num >= 1 ? ValidationResult.Success() : ValidationResult.Error("请输入有效的数字"))
                    .DefaultValue(100)
            );
            int minTargetBlockSum = AnsiConsole.Prompt(
                new TextPrompt<int>("请输入扫描结果最小总方块数：[grey50]（>= 0）[/]")
                    .Validate(num => num >= 0 ? ValidationResult.Success() : ValidationResult.Error("请输入有效的数字"))
                    .DefaultValue(1800)
            );
            int maxThreadsCount = Math.Max(1, Environment.ProcessorCount - 1);
            int threadsCount = AnsiConsole.Prompt(
                new TextPrompt<int>($"请输入线程数：[grey50]（>= 1，<= {maxThreadsCount}）[/]")
                    .Validate(num => num >= 1 && num <= maxThreadsCount ? ValidationResult.Success() : ValidationResult.Error("请输入有效的数字"))
                    .DefaultValue(maxThreadsCount)
            );
            WorldSettings worldSettings = CreateWorldSettings(true);
            List<(string CategoryName, List<(string DisplayName, HashSet<int> BlockValues)> CategoryItems)> targetsCategorized = [
                ("矿物",
                [
                    ("煤矿", [CoalOreBlock.Index]),
                    ("铜矿", [CopperOreBlock.Index]),
                    ("铁矿", [IronOreBlock.Index]),
                    ("硫磺矿", [SulphurOreBlock.Index]),
                    ("钻石矿", [DiamondOreBlock.Index]),
                    ("锗矿", [GermaniumOreBlock.Index]),
                    ("硝石矿", [SaltpeterOreBlock.Index])
                ]),
                ("植物",
                [
                    ("南瓜", [PumpkinBlock.Index]),
                    ("棉花", [Terrain.MakeBlockValue(CottonBlock.Index, 0, CottonBlock.SetSize(CottonBlock.SetIsWild(0, true), 2))])
                ])
            ];
            HashSet<int> gravestoneValues = [];
            for (int i = 0; i < 16; i++) {
                gravestoneValues.Add(Terrain.MakeBlockValue(GravestoneBlock.Index, 0, i));
            }
            targetsCategorized.Add(("物品", [("墓碑", gravestoneValues)]));
            HashSet<int> targetsHashSet = targetsCategorized.SelectMany(category => category.CategoryItems.SelectMany(item => item.BlockValues))
                .ToHashSet();
            ConcurrentQueue<int> seedQueue = new();
            for (int i = 0; i < worldCount; i++) {
                seedQueue.Enqueue(startSeed + i);
            }
            ConcurrentQueue<ScanResult> resultQueue = new();
            AnsiConsole.MarkupLine("启动多线程扫描");
            for (int i = 0; i < threadsCount; i++) {
                Task.Run(() => {
                        using (Worker worker = new(worldSettings, scanRange, targetsHashSet, minTargetBlockSum)) {
                            while (seedQueue.TryDequeue(out int seed)) {
                                try {
                                    resultQueue.Enqueue(worker.Scan(seed));
                                }
                                catch {
                                    //ignore
                                }
                            }
                        }
                    }
                );
            }
            AnsiConsoleProgressBarReporter progressBarReporter = new(false, true, true);
            AnsiConsole.MarkupLine(
                $"| 世界真种子 | 总数 |[grey82]{"矿物".PadCenterMono(54)}[/]|[#40a040]{"植物".PadCenterMono(13)}[/]| 物品 |".Replace("|", "[grey50]|[/]")
            );
            AnsiConsole.MarkupLine(
                "|            |      |[#1F1F1F on grey82] 煤矿 [/]|[#26846D on grey82] 铜矿 [/]|[#87562F on grey70] 铁矿 [/]|[#89843F on grey70] 硫磺矿 [/]|[#0D6D99 on grey70] 钻石矿 [/]|[#6F694F on grey70] 锗矿 [/]|[#FEFEFE on #C2B283] 硝石矿 [/]| [darkorange]南瓜[/] | [#F1F0EA]棉花[/] | [grey82]墓碑[/] |"
                    .Replace("|", "[grey50]|[/]")
            );
            FileStream stream = File.Open(
                $"result_{startSeed}-{startSeed + worldCount - 1}.csv",
                FileMode.OpenOrCreate,
                FileAccess.Write,
                FileShare.Read
            );
            StreamWriter writer;
            if (stream.Length == 0) {
                writer = new StreamWriter(stream, Encoding.UTF8);
                writer.WriteLine("世界真种子,总数,煤矿,铜矿,铁矿,硫磺矿,钻石矿,锗矿,硝石矿,南瓜,棉花,墓碑");
            }
            else {
                stream.Seek(0, SeekOrigin.End);
                writer = new StreamWriter(stream, Encoding.UTF8);
            }
            writer.AutoFlush = false;
            _ = progressBarReporter.StartAsync(["生成并扫描世界"]);
            progressBarReporter.ReportMaxValue(0, worldCount);
            progressBarReporter.ReportStart(0);
            int scannedCount = 0;
            while (seedQueue.Count > 0
                || resultQueue.Count > 0) {
                bool flag = false;
                while (resultQueue.TryDequeue(out ScanResult result)) {
                    progressBarReporter.ReportValue(0, ++scannedCount);
                    Console.Title = $"{scannedCount * 100f / worldCount:F1}% ({scannedCount}/{worldCount}) 生成并扫描世界中";
                    if (result == null) {
                        continue;
                    }
                    flag = true;
                    StringBuilder sb1 = new(500);
                    StringBuilder sb2 = new(70);
                    sb1.Append($"|{result.Seed.ToString().PadLeft(11)} | [yellow]{result.TotalBlocksCount.ToString().PadLeft(4)}[/] |");
                    sb2.Append($"{result.Seed},{result.TotalBlocksCount},");
                    foreach ((string _, List<(string DisplayName, HashSet<int> BlockValues)> categoryItems) in targetsCategorized) {
                        foreach ((string displayName, HashSet<int> blockValues) in categoryItems) {
                            int sum = 0;
                            foreach (int value in blockValues) {
                                if (result.BlocksCount.TryGetValue(value, out int count)) {
                                    sum += count;
                                }
                            }
                            sb1.Append($" {(sum == 0 ? "[grey58]" : "[darkolivegreen3_1]")}{sum.ToString().PadLeft(displayName.Length * 2)}[/] |");
                            sb2.Append($"{sum},");
                        }
                    }
                    AnsiConsole.MarkupLine(sb1.Replace("|", "[grey50]|[/]").ToString());
                    sb2.Remove(sb2.Length - 1, 1);
                    writer.WriteLine(sb2.ToString());
                }
                if (flag) {
                    writer.Flush();
                }
                Task.Delay(1000).Wait();
            }
            writer.Flush();
            writer.Close();
            writer.Dispose();
            stream.Close();
            stream.Dispose();
            progressBarReporter.ReportStop(0);
            _ = progressBarReporter.CompleteAsync();
            AnsiConsole.MarkupLine($"[yellow]已成功生成并扫描 {scannedCount} 个世界！[/][grey50]（总耗时 {progressBarReporter.GetElapsedTime(0):g}）[/]");
            AnsiConsole.MarkupLine($"结果存储在 [blue]{stream.Name}[/]");
            AnsiConsole.MarkupLine("[grey82]目标方块数量不足的世界未显示[/]");
        }

        static WorldInfo CreateVirtualWorldManually() =>
            new() { SerializationVersion = VersionsManager.SerializationVersion, WorldSettings = CreateWorldSettings() };

        static WorldSettings CreateWorldSettings(bool isVirtual = false) {
            WorldSettings worldSettings = new() { OriginalSerializationVersion = VersionsManager.SerializationVersion };
            if (!isVirtual) {
                worldSettings.Name = AnsiConsole.Prompt(
                    new TextPrompt<string>("请输入名称：[grey50]（留空自动生成随机名称）[/]")
                        .Validate(name => string.IsNullOrEmpty(name) || WorldsManager.ValidateWorldName(name)
                            ? ValidationResult.Success()
                            : ValidationResult.Error("名称不合法")
                        )
                        .AllowEmpty()
                );
                if (string.IsNullOrEmpty(worldSettings.Name)) {
                    string name = WorldsManager.NewWorldNames[m_random.Int(0, WorldsManager.NewWorldNames.Count - 1)];
                    worldSettings.Name = name;
                    AnsiConsole.MarkupLine($"已自动生成名称：[blue]{name}[/]");
                }
            }
            worldSettings.TerrainGenerationMode = AnsiConsoleSelectEnum<TerrainGenerationMode>(
                "请选择地形类型",
                key => LanguageControl.Get("Strings", $"TerrainGenerationMode.{key.ToString()}.Name")
            );
            if (!isVirtual) {
                worldSettings.Seed = AnsiConsole.Prompt(new TextPrompt<string>("请输入种子：[grey50]（留空改为设置真种子）[/]").AllowEmpty());
                if (string.IsNullOrEmpty(worldSettings.Seed)) {
                    int? worldSeed = AnsiConsole.Prompt(
                        new TextPrompt<int?>("请输入真种子[grey50]（留空自动生成随机种子）[/]：").DefaultValue(null).ShowDefaultValue(false)
                    );
                    if (worldSeed.HasValue) {
                        worldSettings.WorldSeed = worldSeed.Value;
                    }
                    else {
                        worldSettings.WorldSeed = (int)(long)(Stopwatch.GetTimestamp() * 1000.0);
                        AnsiConsole.MarkupLine($"已自动生成真种子：[blue]{worldSettings.WorldSeed}[/]");
                    }
                }
                else {
                    worldSettings.WorldSeed = EditWorldSeedDialog.SeedToTrueSeed(worldSettings.Seed);
                }
            }
            if (AnsiConsole.Confirm("是否设置更多选项？", false)) {
                worldSettings.StartingPositionMode = AnsiConsoleSelectEnum<StartingPositionMode>(
                    "请选择出生点难度",
                    key => LanguageControl.Get("StartingPositionMode", key.ToString())
                );
                worldSettings.SeaLevelOffset = AnsiConsole.Prompt(
                    new TextPrompt<int>("请输入海平面高度：[grey50]（范围：-64 ~ 191）[/]")
                        .Validate(offset => offset is >= -64 and <= 191 ? ValidationResult.Success() : ValidationResult.Error("请输入 -64 到 191 之间的整数"))
                        .DefaultValue(0)
                );
                worldSettings.TemperatureOffset = AnsiConsole.Prompt(
                    new TextPrompt<int>("请输入温度偏移：[grey50]（范围：-16 ~ 16）[/]")
                        .Validate(offset => offset is >= -16 and <= 16 ? ValidationResult.Success() : ValidationResult.Error("请输入 -16 到 16 之间的整数"))
                        .DefaultValue(0)
                );
                worldSettings.HumidityOffset = AnsiConsole.Prompt(
                    new TextPrompt<int>("请输入湿度偏移：[grey50]（范围：-16 ~ 16）[/]")
                        .Validate(offset => offset is >= -16 and <= 16 ? ValidationResult.Success() : ValidationResult.Error("请输入 -16 到 16 之间的整数"))
                        .DefaultValue(0)
                );
                worldSettings.BiomeSize = AnsiConsole.Prompt(
                    new TextPrompt<float>("请输入生物群系大小：[grey50]（范围：0.01 ~ 32.0）[/]")
                        .Validate(size => size is >= 0.01f and <= 32f ? ValidationResult.Success() : ValidationResult.Error("请输入 0.01 到 32 之间的整数或小数"))
                        .DefaultValue(1f)
                );
            }
            return worldSettings;
        }

        static void Exit() {
            AnsiConsole.MarkupLine("[grey50]请按任意键退出...[/]");
            Console.ReadKey();
            Environment.Exit(0);
        }

        public static T AnsiConsoleSelectEnum<T>(string title, Func<T, string> converter) where T : Enum {
            SelectionPrompt<T> SelectionPrompt = new SelectionPrompt<T>().Title($"{title}：[grey50]（按上/下键来选择）[/]").MoreChoicesText("[grey]上/下还有更多[/]");
            SelectionPrompt.Converter = converter;
            foreach (T value in Enum.GetValues(typeof(T))) {
                SelectionPrompt.AddChoice(value);
            }
            T result = AnsiConsole.Prompt(SelectionPrompt);
            AnsiConsole.MarkupLine($"{title}：[grey50]（按上/下键来选择）[/]");
            AnsiConsole.MarkupLine($"[blue]> {converter(result)}[/]");
            return result;
        }
    }
}