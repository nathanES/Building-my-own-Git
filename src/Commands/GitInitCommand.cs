using Cocona;

namespace codecrafters_git.Commands;

public class GitInitCommand
{
    public GitInitCommand()
    {
    }

    [Command("init", Description = "Init")]
    public void GitInit()
    {
        Directory.CreateDirectory(".git");
        Directory.CreateDirectory(".git/objects");
        Directory.CreateDirectory(".git/refs");
        File.WriteAllText(".git/HEAD", "ref: refs/heads/main\n");
        Console.WriteLine("Initialized git directory");
    }
}