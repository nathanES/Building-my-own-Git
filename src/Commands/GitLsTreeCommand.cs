using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Cocona;
using codecrafters_git.ResultPattern;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Commands;

public class GitLsTreeCommand
{
    private readonly ILogger _logger;
    private const string _treeType = "tree";
    private const string _blobType = "blob";
    private const int _shaLength = 40;

    public GitLsTreeCommand(ILogger logger)
    {
        _logger = logger;
    }

    [Command("ls-tree", Description = "git ls-tree command")]
    public void GitLsTree([Option("name-only")] bool shouldDisplayNameOnly, [Argument(Description = "Id of a tree-ish.")] string treeSha, [Argument(Description = "path")] string? path)
    {
        var lsTreeTreatmentResult = GitLsTreeTreatment(treeSha);
        if (lsTreeTreatmentResult.IsFailure)
        {
            _logger.LogError(JsonSerializer.Serialize(lsTreeTreatmentResult.Errors,
                new JsonSerializerOptions() { WriteIndented = true }));
            return;
        }

        Console.WriteLine(lsTreeTreatmentResult.Response);
    }

    private Result<string> GitLsTreeTreatment(string treeSha)
    {
        return ValidateAndRetrieveTreePath(treeSha)
            .Tap(path => _logger.LogDebug(path))
            .Bind(TryDecompress)
            .Tap(memory => _logger.LogDebug("Decompression Done"))
            .Bind(ValidateAndExtractContent)
            .Tap(d => _logger.LogDebug("Validation and Extraction complete"));
    }
    private Result<string> ValidateAndRetrieveTreePath(string treeSha)
    {
        return ValidateShaFormat(treeSha)
            .Bind(_ => ConstructTreePath(treeSha))
            .Bind(ValidateTreeExist);
    }
    private Result<None> ValidateShaFormat(string sha)
    {
        if (sha?.Length != _shaLength)
            return Result<None>.Create(GitLsTreeErrors.InvalidShaFormat);
        
        return Result<None>.Create(None.Value);
    }
    private Result<string> ConstructTreePath(string treeSha)
    {
        string treePath =  Path.Combine(FilePath.TO_GIT_OBJECTS_FOLDER, treeSha[..2], treeSha[2..]);
        return Result<string>.Create(treePath);
    }
    private Result<string> ValidateTreeExist(string treePath)
    {
        if (!File.Exists(treePath))
            return Result<string>.Create(GitLsTreeErrors.TreeNotFound);

        return Result<string>.Create(treePath);
    }
    private Result<Memory<byte>> TryDecompress(string filePath)
    {
        return Result<Memory<byte>>.TryExecute(() =>
        {
            using Stream compressedStream = new ZLibStream(File.OpenRead(filePath), CompressionMode.Decompress);
            using MemoryStream uncompressedStream = new();
            compressedStream.CopyTo(uncompressedStream);
            return new Memory<byte>(uncompressedStream.GetBuffer())[..(int)uncompressedStream.Length];
        }, ex => GitLsTreeErrors.DecompressionFailed(ex.Message));
    }

    private Result<string> ValidateAndExtractContent(Memory<byte> uncompressedFile)
    {
        var decomposeResult = Decompose(uncompressedFile);
        if (decomposeResult.IsFailure)
            return Result<string>.Create(decomposeResult.Errors);
        
        var (type, length, content) = decomposeResult.Response;
        
        var validationTypeResult = ValidateType(type);
        if (validationTypeResult.IsFailure)
            return Result<string>.Create(validationTypeResult.Errors);

        var validationLengthResult = ValidateLength(content.Length, length);
        if (validationLengthResult.IsFailure)
            return Result<string>.Create(validationLengthResult.Errors);

        return Result<string>.Create(content);
    }
    private Result<None> ValidateType(string type)
    {
        if (type != _treeType)
            return Result<None>.Create(GitLsTreeErrors.TypeInvalid);

        return Result<None>.Create(None.Value);
    }
    private Result<None> ValidateLength(int actualLength, string headerLength)
    {
        if (!int.TryParse(headerLength, out int length))
            return Result<None>.Create(GitLsTreeErrors.HeaderInvalid);

        if (actualLength != length)
            return Result<None>.Create(GitLsTreeErrors.LengthInvalid);

        return Result<None>.Create(None.Value);
    }
    private Result<(string Type, string Length, string Content)> Decompose(Memory<byte> uncompressedFile)
    {
        int nullByteIndex = uncompressedFile.Span.IndexOf((byte)0);
        int spaceByteIndex = uncompressedFile.Span.IndexOf((byte)' ');

        if (nullByteIndex < 0 || spaceByteIndex < 0 || spaceByteIndex >= nullByteIndex)
        {
            return Result<(string, string, string)>.Create(GitLsTreeErrors.HeaderInvalid);
        }

        string type = Encoding.UTF8.GetString(uncompressedFile[..spaceByteIndex].Span);
        string length = Encoding.UTF8.GetString(uncompressedFile[(spaceByteIndex + 1)..nullByteIndex].Span);
        string content = Encoding.UTF8.GetString(uncompressedFile[(nullByteIndex + 1)..].Span);

        return Result<(string, string, string)>.Create((type, length, content));

    }
}
    
public static class GitLsTreeErrors
{
    public static readonly Error InvalidShaFormat = new Error("InvalidShaFormat", "The SHA-1 hash must be exactly 40 characters long.");
    public static readonly Error TreeNotFound = new Error("TreeNotFound", "Tree Not Found");
    public static readonly Error TypeInvalid = new Error("TypeInvalid", "The object is not a valid 'blob' type.");
    public static readonly Error LengthInvalid = new Error("LengthInvalid", "Length does not match the expected length.");
    public static readonly Error HeaderInvalid = new Error("HeaderInvalid", "Header is malformed.");
    public static Error DecompressionFailed(string message) => new Error("DecompressionFailed", $"Failed to decompress: {message}");
}