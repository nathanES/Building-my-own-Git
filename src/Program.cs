using Cocona;
using codecrafters_git.Commands;
using codecrafters_git.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = CoconaApp.CreateBuilder(configureOptions: options => { options.TreatPublicMethodsAsCommands = false; });
// builder.Logging.ClearProviders();
// builder.Logging.AddColorConsoleLogger(configuration =>
// {
//     configuration.LogLevelToColorMap[LogLevel.Warning] = ConsoleColor.DarkCyan;
//     configuration.LogLevelToColorMap[LogLevel.Error] = ConsoleColor.DarkRed;
// });
builder.Services.AddSingleton<ILogger>(logger => new ColorConsoleLogger(nameof(codecrafters_git), (() => new ColorConsoleLoggerConfiguration()
{
    LogLevelToColorMap = new Dictionary<LogLevel, ConsoleColor>()
    {
        { LogLevel.Debug, ConsoleColor.Blue },
        { LogLevel.Information, ConsoleColor.Green },
        { LogLevel.Warning, ConsoleColor.DarkMagenta },
        { LogLevel.Error, ConsoleColor.DarkYellow },
        { LogLevel.Critical, ConsoleColor.Red }
    }
})));
var app = builder.Build();

app.AddCommands<GitInitCommand>();
app.AddCommands<GitCatFileCommand>();
app.AddCommands<GitHashObjectCommand>();
app.AddCommands<GitLsTreeCommand>();
app.Run();