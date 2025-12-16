using System;
using Engine;
using Spectre.Console;

namespace SurvivalcraftTerrainScanner {
    public class AnsiConsoleLogSink : ILogSink {
        public void Log(LogType type, string message) {
            AnsiConsole.MarkupLine(
                $"[green]{DateTime.Now:HH:mm:ss.fff}[/] {type switch {
                    LogType.Debug => "[mediumpurple2][[DEBUG]][/]",
                    LogType.Verbose => "[dodgerblue1][[INFO]][/]",
                    LogType.Information => "[dodgerblue1][[INFO]][/]",
                    LogType.Warning => "[yellow][[WARN]][/]",
                    LogType.Error => "[red][[ERROR]][/]",
                    _ => string.Empty
                }}: {message.Replace("[", "[[").Replace("]", "]]")}"
            );
        }
    }
}