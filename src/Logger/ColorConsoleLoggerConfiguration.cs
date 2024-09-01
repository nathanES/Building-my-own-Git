using Microsoft.Extensions.Logging;

namespace codecrafters_git.Utils;

public class ColorConsoleLoggerConfiguration
{
    public Dictionary<LogLevel, ConsoleColor> LogLevelToColorMap { get; set; } = new()
    {
        [LogLevel.Information] = ConsoleColor.Green
    };
}