using Cocona;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Commands;

public class GitInitCommand
{
    private readonly ILogger _logger;

    public GitInitCommand(ILogger logger)
    {
        _logger = logger;
    }

    [Command("init", Description = "git init command")]
    public void GitInit()
    {
        Directory.CreateDirectory(".git");
        Directory.CreateDirectory(".git/objects");
        Directory.CreateDirectory(".git/refs");
        File.WriteAllText(".git/HEAD", "ref: refs/heads/main\n");
        Console.WriteLine("Initialized git directory");
    }
}