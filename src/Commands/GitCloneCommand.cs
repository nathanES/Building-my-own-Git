using Cocona;
using codecrafters_git.Services;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Commands;
public class GitCloneCommand(ILogger logger, IGitService gitService)
{
    private readonly ILogger _logger = logger;
    private readonly IGitService _gitService = gitService;

    [Command("clone", Description = "Git clone command")]
    public async Task Clone([Argument(Description = "uri")] string uri)
    {
        
    }
}