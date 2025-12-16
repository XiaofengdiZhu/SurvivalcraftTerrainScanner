using System.Threading.Tasks;
using Spectre.Console;

namespace SurvivalcraftTerrainScanner {
    public class AnsiConsoleStatusReporter {
        readonly Status _status;
        StatusContext _context;
        readonly TaskCompletionSource _completionSource = new();

        public AnsiConsoleStatusReporter(bool autoFresh = true, Spinner spinner = null, Style spinnerStyle = null) => _status = AnsiConsole.Status()
            .AutoRefresh(autoFresh)
            .Spinner(spinner ?? Spinner.Known.Default)
            .SpinnerStyle(spinnerStyle ?? Color.Yellow);

        public async Task StartAsync(string status) => await _status.StartAsync(
            status,
            async ctx => {
                _context = ctx;
                await _completionSource.Task;
            }
        );

        public void ReportStatus(string status) {
            if (!string.IsNullOrEmpty(status)) {
                _context?.Status = status;
            }
        }

        public void ReportSpinner(Spinner spinner) {
            if (spinner != null) {
                _context?.Spinner = spinner;
            }
        }

        public void ReportSpinnerStyle(Style spinnerStyle) {
            if (spinnerStyle != null) {
                _context?.SpinnerStyle = spinnerStyle;
            }
        }

        public void ForceRefresh() => _context?.Refresh();

        public async Task CompleteAsync() {
            if (_context != null) {
                _completionSource.TrySetResult();
                _context = null;
                // 等待一小段时间让用户看到完成状态
                await Task.Delay(150);
            }
        }

        public void Dispose() {
            _completionSource.TrySetCanceled();
            _context = null;
        }
    }
}