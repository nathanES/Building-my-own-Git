using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Cocona;
using codecrafters_git.ResultPattern;
using codecrafters_git.Services;
using codecrafters_git.Utils;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Commands;

public class GitHashObjectCommand
{
    private readonly ILogger _logger;
    private readonly IGitService _gitService;

    public GitHashObjectCommand(ILogger logger, IGitService gitService)
    {
        _logger = logger;
        _gitService = gitService;
    }

    [Command("hash-object", Description = "Git hash-object command")]
    public async Task GitHashObject([Argument(Description = "File Path")] string filePath,
        [Option('w', Description = "write to the objects directory")] bool shouldWrite)
    {
        _logger.LogDebug(Directory.GetCurrentDirectory());

#if DEBUG
        filePath = "../../../" + filePath;
#endif

        var hashObjectTreatmentResult = HashObjectTreatment(filePath, shouldWrite)
            .Tap(hash => _logger.LogInformation($"Hash successfully created: {hash}"))
            .TapError(errors => LogError(errors));
        Console.WriteLine(hashObjectTreatmentResult.Response);
        
    }

    private Result<string> HashObjectTreatment(string filePath, bool shouldWrite)
    {
        return _gitService.GenerateBlobAsync(filePath)
            .TapAsync(blobResult =>
                _logger.LogDebug(
                    $"Generated Blob : {JsonSerializer.Serialize(blobResult.Response, new JsonSerializerOptions() { WriteIndented = true })}"))
            .TapErrorAsync(LogError)
            .Bind(blob => shouldWrite ? _gitService.WriteInDataBaseAsync(blob.Content).Bind(_=> Result<string>.Success(blob.Sha))
                : Result<string>.Success(blob.Sha));
    }
    
    // private async Task LogError(IEnumerable<Error> errors)
    // {
    //     _logger.LogError(JsonSerializer.Serialize(errors,
    //         new JsonSerializerOptions() { WriteIndented = true }));
    //     return; 
    // }
    private async Task LogError(IEnumerable<Error> errors)
    {
        _logger.LogError(JsonSerializer.Serialize(errors,
            new JsonSerializerOptions() { WriteIndented = true }));
    }
}