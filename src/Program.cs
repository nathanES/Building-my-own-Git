using Cocona;
using codecrafters_git.Commands;

var builder = CoconaApp.CreateBuilder(configureOptions: options =>
{
    options.TreatPublicMethodsAsCommands = false;
});

var app = builder.Build();
app.AddCommands<GitInitCommand>();
app.AddCommands<GitCatFileCommand>();

app.Run();