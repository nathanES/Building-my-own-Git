using Microsoft.Extensions.Logging;

namespace codecrafters_git.Utils;

public class ColorConsoleLogger(string name, Func<ColorConsoleLoggerConfiguration> getCurrentConfig) : ILogger
{
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if(!IsEnabled(logLevel))
            return;
        
        ColorConsoleLoggerConfiguration config = getCurrentConfig();
        
        ConsoleColor originalColor = Console.ForegroundColor;

        Console.ForegroundColor = config.LogLevelToColorMap[logLevel];
        Console.WriteLine($"[{eventId.Id,2}: {logLevel,-12}]");
            
        Console.ForegroundColor = originalColor;
        Console.Write($"     {name} - ");

        Console.ForegroundColor = config.LogLevelToColorMap[logLevel];
        Console.Write($"{formatter(state, exception)}");
            
        Console.ForegroundColor = originalColor;
        Console.WriteLine();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        // return false;
#if DEBUG
        return true;
#endif
        return logLevel == LogLevel.None;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return default!;
    }
}