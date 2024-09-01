using Cocona;
using codecrafters_git.Commands;
using codecrafters_git.Utils;
using Microsoft.Extensions.DependencyInjection;

var builder = CoconaApp.CreateBuilder(configureOptions: options =>
{
    options.TreatPublicMethodsAsCommands = false;
});
builder.Services.AddSingleton<ICustomWriter, CustomWriter>();

var app = builder.Build();
app.AddCommands<GitInitCommand>();
app.AddCommands<GitCatFileCommand>();

app.Run();