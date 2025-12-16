using System;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace SurvivalcraftTerrainScanner {
    public class AnsiConsoleProgressBarReporter {
        class DownloadedColumn : ProgressColumn {
            public int ColumnWidth = 5;

            public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime) => new Align(
                new Markup(task.IsFinished ? $"[green]{task.Value}/{task.MaxValue}[/]" : $"{task.Value}[grey]/[/]{task.MaxValue}"),
                HorizontalAlignment.Right
            );

            public override int? GetColumnWidth(RenderOptions options) => ColumnWidth;
        }

        public sealed class ElapsedTimeColumn : ProgressColumn {
            protected override bool NoWrap => true;

            public Style Style { get; set; } = Color.Blue;

            public int ColumnWidth = 8;

            /// <inheritdoc />
            public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime) {
                TimeSpan? elapsedTime = task.ElapsedTime;
                if (!elapsedTime.HasValue) {
                    ColumnWidth = 8;
                    return new Markup("--:--:--");
                }
                if (elapsedTime.Value.TotalHours > 99.0) {
                    ColumnWidth = 8;
                    return new Markup("**:**:**");
                }
                if (elapsedTime.Value.TotalHours > 1.0) {
                    ColumnWidth = 8;
                    return new Text($"{elapsedTime.Value:hh\\:mm\\:ss}", Style ?? Style.Plain);
                }
                if (elapsedTime.Value.TotalMinutes > 1.0) {
                    ColumnWidth = 5;
                    return new Text($"{elapsedTime.Value:mm\\:ss}", Style ?? Style.Plain);
                }
                ColumnWidth = elapsedTime.Value.TotalSeconds > 10 ? 7 : 6;
                return new Text($"{elapsedTime.Value:s\\.fff\\s}", Style ?? Style.Plain);
            }

            /// <inheritdoc />
            public override int? GetColumnWidth(RenderOptions options) => ColumnWidth;
        }

        public sealed class RemainingTimeColumn : ProgressColumn {
            protected override bool NoWrap => true;
            public Style Style { get; set; } = Color.Blue;

            public int ColumnWidth = 8;

            public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime) {
                TimeSpan? remaining = task.RemainingTime;
                if (!remaining.HasValue) {
                    ColumnWidth = 8;
                    return new Markup("--:--:--");
                }
                if (remaining.Value.TotalHours > 99.0) {
                    ColumnWidth = 8;
                    return new Markup("**:**:**");
                }
                if (remaining.Value.TotalHours > 1.0) {
                    ColumnWidth = 8;
                    return new Text($"{remaining.Value:hh\\:mm\\:ss}", Style ?? Style.Plain);
                }
                if (remaining.Value.TotalMinutes > 1.0) {
                    ColumnWidth = 5;
                    return new Text($"{remaining.Value:mm\\:ss}", Style ?? Style.Plain);
                }
                ColumnWidth = remaining.Value.TotalSeconds > 10 ? 7 : 6;
                return new Text($"{remaining.Value:s\\.fff\\s}", Style ?? Style.Plain);
            }

            public override int? GetColumnWidth(RenderOptions options) => ColumnWidth;
        }

        readonly Progress _progress;
        ProgressContext _context;
        ProgressTask[] _tasks;
        readonly DownloadedColumn _downloadedColumn = new();
        readonly TaskCompletionSource _completionSource = new();

        public AnsiConsoleProgressBarReporter(bool autoClear = false,
            bool autoRefresh = true,
            bool hideCompleted = false,
            params ProgressColumn[] columns) => _progress = AnsiConsole.Progress()
            .AutoClear(autoClear)
            .AutoRefresh(autoRefresh)
            .HideCompleted(hideCompleted)
            .Columns(
                columns.Length > 0
                    ? columns
                    : [
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        _downloadedColumn,
                        new PercentageColumn(),
                        new ElapsedTimeColumn(),
                        new RemainingTimeColumn(),
                        new SpinnerColumn()
                    ]
            );

        public async Task StartAsync(string[] stages) => await _progress.StartAsync(async ctx => {
                _context = ctx;
                _tasks = new ProgressTask[stages.Length];
                // 初始化所有任务
                for (int i = 0; i < stages.Length; i++) {
                    string stage = stages[i];
                    _tasks[i] = ctx.AddTask($"{stage}", false);
                    _tasks[i].Value = 0;
                }

                // 等待外部完成信号
                await _completionSource.Task;
            }
        );

        public void ReportStart(int stageIndex) {
            if (_context == null
                || stageIndex < 0
                || stageIndex >= _tasks.Length) {
                return;
            }
            _tasks[stageIndex].StartTask();
        }

        public void ReportStop(int stageIndex) {
            if (_context == null
                || stageIndex < 0
                || stageIndex >= _tasks.Length) {
                return;
            }
            _tasks[stageIndex].StopTask();
        }

        public void ReportValue(int stageIndex, int value) {
            if (_context == null
                || stageIndex < 0
                || stageIndex >= _tasks.Length) {
                return;
            }
            _tasks[stageIndex].Value = value;
        }

        public void ReportMaxValue(int stageIndex, int maxValue) {
            if (_context == null
                || stageIndex < 0
                || stageIndex >= _tasks.Length) {
                return;
            }
            int requiredWidth = maxValue.ToString().Length * 2 + 1;
            if (requiredWidth > _downloadedColumn.ColumnWidth) {
                _downloadedColumn.ColumnWidth = requiredWidth;
            }
            _tasks[stageIndex].MaxValue = maxValue;
        }

        public async Task CompleteAsync() {
            if (_context != null) {
                _completionSource.TrySetResult();
                _context = null;
                // 等待一小段时间让用户看到完成状态
                await Task.Delay(150);
            }
        }

        public TimeSpan GetElapsedTime(int stageIndex) {
            if (stageIndex < 0
                || stageIndex >= _tasks.Length) {
                return TimeSpan.Zero;
            }
            return _tasks[stageIndex].ElapsedTime ?? TimeSpan.Zero;
        }

        public void Dispose() {
            _completionSource.TrySetCanceled();
            _context = null;
        }
    }
}