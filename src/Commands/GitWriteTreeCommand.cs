using System.Text.Json;
using Cocona;
using codecrafters_git.ResultPattern;
using codecrafters_git.Services;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Commands;

public class GitWriteTreeCommand
{
    private readonly ILogger _logger;
    private readonly IGitService _gitService;

    public GitWriteTreeCommand(ILogger logger, IGitService gitService)
    {
        _logger = logger;
        _gitService = gitService;
    }

    [Command("write-tree", Description = "git write-tree command")]
    public async Task GitWriteTree()
    {
#if DEBUG
        var currentDirectoryResult = GoBackDirectories(Directory.GetCurrentDirectory(), 3);
        if (currentDirectoryResult.IsFailure)
        {
            await LogError(currentDirectoryResult.Errors);
            return;
        }

        string currentDirectory = currentDirectoryResult.Response;
#else
        string currentDirectory = Directory.GetCurrentDirectory();
#endif
        _logger.LogDebug($"Current Directory: {currentDirectory}");
        _ = await _gitService.WriteTreeAsync(currentDirectory)
            .TapAsync(treeResult => Console.WriteLine(treeResult.Response.Sha))
            .TapErrorAsync(LogError);

    }
    private async Task LogError(IEnumerable<Error> errors)
    {
        _logger.LogError(JsonSerializer.Serialize(errors,
            new JsonSerializerOptions() { WriteIndented = true }));
        return;
    }
    public Result<string> GoBackDirectories(string currentPath, int backNumber)
    {
        if (backNumber < 0)
            return Result<string>.Failure(GitWriteTreeErrors.GoBackDirectoriesBackNumberError);

        var directoryInfo = new DirectoryInfo(currentPath);
        for (int i = 0; i < backNumber; i++)
        {
            directoryInfo = directoryInfo.Parent;
            if (directoryInfo == null)
            {
                return Result<string>.Failure(GitWriteTreeErrors.GoBackDirectoriesExceedRootError);
            }
        }

        return Result<string>.Success(directoryInfo.FullName);
    }
}

public static class GitWriteTreeErrors
{
    public static readonly Error GoBackDirectoriesBackNumberError =
        new Error("GoBackDirectoriesBackNumberError", "Number of directories to go back cannot be negative.");

    public static readonly Error GoBackDirectoriesExceedRootError =
        new Error("GoBackDirectoriesExceedRootError", "Exceeded the root directory.");
}