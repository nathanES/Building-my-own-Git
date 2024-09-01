using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Utils;

public static class ILoggerExtensions
{
    public static void Log(this ILogger logger, LogLevel logLevel, object content)
    {
        logger.Log(logLevel,JsonSerializer.Serialize(content, options: new JsonSerializerOptions(){ WriteIndented = true}));
    }
    public static void LogTrace(this ILogger logger, object content)
    {
        logger.Log(LogLevel.Trace, content);
    }
    public static void LogDebug(this ILogger logger, object content)
    {
        logger.Log(LogLevel.Debug, content);
    }
    public static void LogInfo(this ILogger logger, object content)
    {
        logger.Log(LogLevel.Information, content);
    }
    public static void LogWarning(this ILogger logger, object content)
    {
        logger.Log(LogLevel.Warning, content);
    }
    public static void LogError(this ILogger logger, object content)
    {
        logger.Log(LogLevel.Error, content);
    }
    public static void LogCritical(this ILogger logger, object content)
    {
        logger.Log(LogLevel.Critical, content);
    }
}