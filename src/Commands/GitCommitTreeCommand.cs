using System.Text.Json;
using System.Threading.Channels;
using Cocona;
using codecrafters_git.ResultPattern;
using codecrafters_git.Services;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Commands;

public class GitCommitTreeCommand(ILogger logger, IGitService gitService)
{
    private readonly ILogger _logger = logger;
    private readonly IGitService _gitService = gitService;

    //$ ./your_program.sh commit-tree <tree_sha> -p <commit_sha> -m <message>
    [Command("commit-tree", Description = "Git commit-tree command")]
    public async Task GitCommitTree([Argument(Description = "Tree Sha")] string treeSha, 
        [Option('p', Description = "Commit Sha")] string commitSha, 
        [Option('m', Description ="Message")] string message)
    {
       _ = await _gitService.CommitTreeAsync(treeSha, commitSha, message)
            .TapAsync(commitTreeResult => Console.WriteLine(commitTreeResult.Response))
            .TapErrorAsync(LogError);
    }
    private async Task LogError(IEnumerable<Error> errors)
    {
        _logger.LogError(JsonSerializer.Serialize(errors,
            new JsonSerializerOptions() { WriteIndented = true }));
        return;
    }
}