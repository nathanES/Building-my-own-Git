using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Cocona;
using codecrafters_git.ResultPattern;
using codecrafters_git.Services;
using codecrafters_git.Utils;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Commands;

public class GitLsTreeCommand
{
    private readonly ILogger _logger;
    private readonly IGitService _gitService;

    public GitLsTreeCommand(ILogger logger, IGitService gitService)
    {
        _logger = logger;
        _gitService = gitService;
    }

    [Command("ls-tree", Description = "git ls-tree command")]
    public async Task GitLsTree([Option("name-only")] bool shouldDisplayNameOnly,
        [Argument(Description = "Id of a tree-ish.")] string treeSha, [Argument(Description = "path")] string? path)
    {
        if (shouldDisplayNameOnly)
        {
            await GitLsTreeTreatmentNameOnly(treeSha)
                .TapErrorAsync(LogError);
            return;
        }            
            
        await GitLsTreeTreatment(treeSha)
            .TapErrorAsync(LogError);;
        return;
    }

    private async Task LogError(IEnumerable<Error> errors)
    {
        _logger.LogError(JsonSerializer.Serialize(errors,
            new JsonSerializerOptions() { WriteIndented = true }));
        return; 
    }
    public async Task<Result<None>> GitLsTreeTreatmentNameOnly(string treeSha)
    {
        var result = await _gitService.GetTreeAsync(treeSha)
            .TapAsync(taskResultTree =>
            {
                DisplayTreeName(taskResultTree.Response);
            });
        
        return result.Bind(_ => Result<None>.Success(None.Value)); 
    }

    private void DisplayTreeName(Tree tree)
    {
        //TODO do the display tree Name
        foreach (var entry in tree.Entries)
        {
            Console.WriteLine($"{entry.Path}");
        }
    }
    private void DisplayTree(Tree tree)
    {
        foreach (var entry in tree.Entries)
        {
            Console.Write($"{entry.Mode} {entry.Path}\0{entry.Sha}");
        }
       
    }
    private async Task<Result<None>> GitLsTreeTreatment(string treeSha)
    {
        var result = await _gitService.GetTreeAsync(treeSha)
            .TapAsync(taskResultTree =>
            {
                DisplayTree(taskResultTree.Response);
            });
        return result.Bind(_ => Result<None>.Success(None.Value)); 
    }
}