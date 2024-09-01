using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Cocona;
using codecrafters_git.ResultPattern;
using codecrafters_git.Utils;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Commands;

public class GitHashObjectCommand
{
    private readonly ILogger _logger;
    private const string _blobType = "blob";

    public GitHashObjectCommand(ILogger logger)
    {
        _logger = logger;
    }

    [Command("hash-object", Description = "Git hash-object command")]
    public void GitHashObject([Argument(Description = "File Path")] string filePath,
        [Option('w', Description = "write  to the objects directory")] bool shouldWrite)
    {
        _logger.LogDebug(Directory.GetCurrentDirectory());

#if DEBUG
        filePath = "../../../" + filePath;
#endif

        var hashObjectTreatmentResult = HashObjectTreatment(filePath, shouldWrite)
            .Tap(hash => _logger.LogInformation($"Hash successfully created: {hash}"));
        if (hashObjectTreatmentResult.IsFailure)
        {
            _logger.LogError(hashObjectTreatmentResult.Errors);
            return;
        }
        Console.WriteLine(hashObjectTreatmentResult.Response);
        
    }

    private Result<string> HashObjectTreatment(string filePath, bool shouldWrite)
    {
        return ValidateFileExist(filePath)
            .Bind(_ => GenerateBlob(filePath)
                .Tap(blob => _logger.LogDebug($"Generated Blob : {Encoding.UTF8.GetString(blob)}"))
                .Bind(blob => GenerateHash(blob)
                    .Tap(hash => _logger.LogInformation($"Generated Hash : {hash}"))
                    .Bind(hash => shouldWrite ? WriteBlob(hash, blob)
                            .Bind(_ => Result<string>.Create(hash)) 
                        : Result<string>.Create(hash))));
    }

    private Result<None> WriteBlob(string hash, byte[] blob)
    {
        return CreateBlobDirectory(hash)
            .Tap(_ => _logger.LogDebug("Blob Directory Created"))
            .Bind(_ => CreateBlobFile(hash, blob))
            .Tap(_ => _logger.LogDebug("Blob File Created"));
    }

    private Result<string> ValidateFileExist(string filePath)
    {
        if (!File.Exists(filePath))
            return Result<string>.Create(GitHashObjectErrors.FileNotFound);

        return Result<string>.Create(filePath);
    }

    public Result<byte[]> GenerateBlob(string filePath)
    {
        string content = File.ReadAllText(filePath);
        _logger.LogDebug(content);

        string header = $"{_blobType} {content.Length}\0";
        _logger.LogDebug(header);

        return Result<byte[]>.Create(Encoding.UTF8.GetBytes(header + content)); 
    }
    public Result<string> GenerateHash(byte[] blob)
    {
        var sha1 = SHA1.HashData(blob);
        string hash = Convert.ToHexString(sha1).ToLower();
        _logger.LogDebug($"Generated Hash: {hash}");
        
        return Result<string>.Create(hash);
    }

    public Result<None> CreateBlobDirectory(string hash)
    {
        Directory.CreateDirectory(GetBlobDirectoryPath(hash));
        return Result<None>.Create(None.Value);
    }

    private Result<None> CreateBlobFile(string hash, byte[] blob)
    {
        return Result<None>.TryExecute(() =>
        {
            string blobPath = Path.Combine(GetBlobDirectoryPath(hash), hash[2..]);
            using ZLibStream zLibStream = new ZLibStream(new FileStream(blobPath, FileMode.Create),
                CompressionMode.Compress);
            zLibStream.Write(blob, 0, blob.Length);
            _logger.LogInformation($"Blob Written to : {blobPath}");
            return None.Value;
        }, ex => GitHashObjectErrors.WriteFileError($"{ex.Message} - Path: {GetBlobDirectoryPath(hash)}/{hash[2..]}"));
    }

    private string GetBlobDirectoryPath(string hash) => Path.Combine(FilePath.TO_GIT_OBJECTS_FOLDER, hash[..2]);
}

public class GitHashObjectErrors
{
    public static readonly Error FileNotFound = new Error("FileNotFound", "File not found");
    public static Error CompressionFailed(string message) => new Error("CompressionFailed", $"Failed to compress the file: {message}");
    public static Error WriteFileError(string message) =>
        new Error("WriteFileError", $"Error during the writing process : {message}");
}