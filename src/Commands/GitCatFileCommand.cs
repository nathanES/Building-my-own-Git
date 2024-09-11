using Cocona;
using codecrafters_git.ResultPattern;
using codecrafters_git.Services;
using codecrafters_git.Utils;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Commands;

public class GitCatFileCommand
{
    private readonly ILogger _logger;
    private readonly IGitService _gitService;
    private const int _blobShaLength = 40;
    private const string _blobType = "blob";

    public GitCatFileCommand(ILogger logger, IGitService gitService)
    {
        _logger = logger;
        _gitService = gitService;
    }

    [Command("cat-file", Description = "Git cat-file command")]
    public async Task GitCatFile([Option('p', Description = "BlobSha")] string blobSha)
    {
        var catFileTreatmentResult = await PrettyPrintTreatment(blobSha);
        if (catFileTreatmentResult.IsFailure)
        {
            _logger.LogError(catFileTreatmentResult.Errors);
            return;
        }
    }

    private async Task<Result<None>> PrettyPrintTreatment(string blobSha)
    {
       var result = await _gitService.GetBlobAsync(blobSha)
            .TapAsync(taskResultBlob =>
            {
                Console.WriteLine(taskResultBlob.Response.Content);
            });
       return result.Bind(_ => Result<None>.Success(None.Value));
    }
}
