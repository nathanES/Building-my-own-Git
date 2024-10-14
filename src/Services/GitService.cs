using System.IO.Compression;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using codecrafters_git.ResultPattern;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Services;

public interface IGitService
{
    Task<Result<Blob>> GetBlobAsync(string sha);
    Task<Result<Tree>> GetTreeAsync(string sha);
    Task<Result<None>> WriteBlobInDataBaseAsync(Blob blob);
    Task<Result<Blob>> GenerateBlobAsync(string path);
    Task<Result<Tree>> WriteTreeAsync(string path);
}

public class GitService : IGitService
{
    private const int _shaLength = 40;

    private readonly ILogger _logger;
    private readonly string _pathToGitObjectFolder;

    public GitService(string pathToGitObjectFolder, ILogger logger)
    {
        _logger = logger;
        _pathToGitObjectFolder = pathToGitObjectFolder;
    }

    public async Task<Result<Blob>> GetBlobAsync(string sha)
    {
        return await ReadGitObjectAsync(sha)
            .BindAsync(TryParseBlobAsync)
            .TapAsync(_ => _logger.LogDebug("Blob successfully parsed and validated"))
            .TapErrorAsync(error => _logger.LogError($"Error parsing blob: {error}"));
    }

    public async Task<Result<Tree>> GetTreeAsync(string sha)
    {
        return await ReadGitObjectAsync(sha)
            .BindAsync(TryParseTreeAsync)
            .TapAsync(_ => _logger.LogDebug("Blob successfully parsed and validated"))
            .TapErrorAsync(error => _logger.LogError($"Error parsing blob: {error}"));
    }

    public async Task<Result<None>> WriteBlobInDataBaseAsync(Blob blob)
    {
        return await CreateDirectory(blob.Sha)
            .BindAsync(path => AddBlobHeader(blob)
                .BindAsync(data => TryWriteDataAsync(path, blob.Sha[2..], data)));
    }

    private async Task<Result<byte[]>> AddBlobHeader(Blob blob)
    {
        var header = Encoding.UTF8.GetBytes($"{GitObjectType.Blob.Value} {blob.Content.Length}\0");

        var result = new byte[header.Length + blob.Content.Length];
        header.CopyTo(result, 0);
        blob.Content.CopyTo(result, header.Length);
        return Result<byte[]>.Success(result);
    }

    private Result<string> CreateDirectory(string sha)
    {
        string directoryPath = Path.Combine(_pathToGitObjectFolder, sha[..2]);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        return Result<string>.Success(directoryPath);
    }

    public async Task<Result<Blob>> GenerateBlobAsync(string path)
    {
        return await ValidateExist(path)
            .BindAsync(GenerateBlobData)
            .BindAsync(TryParseBlobAsync);
    }

    public async Task<Result<Tree>> WriteTreeAsync(string path)
    {
        var filesFullNameResult = await GetFilesFullName(path);
        if (filesFullNameResult.IsFailure)
            return Result<Tree>.Failure(filesFullNameResult.Errors);
        _logger.LogDebug($"{nameof(filesFullNameResult)} : OK");

        var directoriesFullNameResult = await GetDirectoriesFullName(path);
        if (directoriesFullNameResult.IsFailure)
            return Result<Tree>.Failure(directoriesFullNameResult.Errors);
        _logger.LogDebug($"{nameof(directoriesFullNameResult)} : OK");
 
        var treeEntries = new List<Tree.TreeEntry>();
        
        foreach (var fileFullName in filesFullNameResult.Response)
        {
            var blobResult = await GenerateBlobAsync(fileFullName);
            if (blobResult.IsFailure)
                return Result<Tree>.Failure(blobResult.Errors);
            var blob = blobResult.Response;
            treeEntries.Add(new Tree.FileEntry
            {
                Path = Path.GetFileName(fileFullName),
                Sha = blob.Sha
            });
            
            var writeBlobResult = await WriteBlobInDataBaseAsync(blob);
            if (writeBlobResult.IsFailure)
                return Result<Tree>.Failure(writeBlobResult.Errors);
        }
        _logger.LogDebug($"{nameof(filesFullNameResult)} : Processed");
 
        foreach (var directoryFullName in directoriesFullNameResult.Response)
        {
            var subTreeResult = await WriteTreeAsync(directoryFullName);
            if (subTreeResult.IsFailure)
                return Result<Tree>.Failure(subTreeResult.Errors);

            var subTree = subTreeResult.Response;

            treeEntries.Add(new Tree.DirectoryEntry()
            {
                Path = Path.GetFileName(directoryFullName),
                Sha = subTree.Sha
            });
        }
        _logger.LogDebug($"{nameof(directoriesFullNameResult)} : Processed");

        var tree = new Tree
        {
            Entries = treeEntries
        };

        var treeData = SerializeTree(treeEntries);
        _logger.LogDebug($"{nameof(treeData)} : Serialized");

        var shaResult = CalculateSha(treeData);
        if (shaResult.IsFailure)
            return Result<Tree>.Failure(shaResult.Errors);
        _logger.LogDebug($"{nameof(shaResult)} : OK");


        tree.Sha = shaResult.Response;

        var directoryResult = CreateDirectory(tree.Sha);
        if (directoryResult.IsFailure)
            return Result<Tree>.Failure(directoryResult.Errors);
        _logger.LogDebug($"{nameof(directoryResult)} : Created");

        var writeTreeResult = await TryWriteDataAsync(directoryResult.Response, tree.Sha[2..], treeData);
        if (writeTreeResult.IsFailure)
            return Result<Tree>.Failure(writeTreeResult.Errors);
        _logger.LogDebug($"{nameof(writeTreeResult)} : Writed");

        return Result<Tree>.Success(tree); 
    }
    private byte[] SerializeTree(List<Tree.TreeEntry> entries)
    {
        using var memoryStream = new MemoryStream();

        foreach (var entry in entries)
        {
            // Each entry follows the format "mode path\0sha"
            var entryLine = $"{entry.Mode} {entry.Path}\0";
            var entryBytes = Encoding.UTF8.GetBytes(entryLine);

            memoryStream.Write(entryBytes, 0, entryBytes.Length);

            // Write the SHA (as 20 bytes)
            var shaBytes = Enumerable.Range(0, entry.Sha.Length / 2)
                .Select(x => Convert.ToByte(entry.Sha.Substring(x * 2, 2), 16))
                .ToArray();
            memoryStream.Write(shaBytes, 0, shaBytes.Length);
        }

        return memoryStream.ToArray();
    }
    private Task<Result<List<string>>> GetFilesFullName(string path)
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(path);
        var fileInfos = directoryInfo.EnumerateFiles();
        var filesName = new List<string>();
        foreach (var fileInfo in fileInfos)
        {
            filesName.Add(fileInfo.FullName);
        }

        return Task.FromResult(Result<List<string>>.Success(filesName));
    }

    private Task<Result<List<string>>> GetDirectoriesFullName(string path)
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(path);
        var directoriesInfo = directoryInfo.EnumerateDirectories();
        var directoriesFullName = new List<string>();
        foreach (var directoryInfoElement in directoriesInfo)
        {
            directoriesFullName.Add(directoryInfoElement.FullName);
        }

        return Task.FromResult(Result<List<string>>.Success(directoriesFullName));
    }

    private async Task<Result<Tree>> TryParseTreeAsync(byte[] data)
    {
        try
        {
            var tree = new Tree();
            var headerEndIndex = Array.IndexOf(data, (byte)0);
            string header = Encoding.UTF8.GetString(data, 0, headerEndIndex);
            string[] headerParts = header.Split(' ');
            int index = headerEndIndex + 1;

            while (index < data.Length)
            {
                int spaceIndex = Array.IndexOf(data, (byte)' ', index);
                int nullIndex = Array.IndexOf(data, (byte)0, spaceIndex);

                string mode = Encoding.UTF8.GetString(data.AsSpan(index, spaceIndex - index));
                string path = Encoding.UTF8.GetString(data.AsSpan(spaceIndex + 1, nullIndex - spaceIndex - 1));
                string sha = BitConverter.ToString(data, nullIndex + 1, 20).Replace("-", "").ToLower();

                tree.Entries.Add(new Tree.TreeEntry
                {
                    Path = path,
                    Mode = mode,
                    Sha = sha,
                    Type = DetermineType(mode)
                });

                index = nullIndex + 21; // Move to the next entry start
            }

            tree.Sha = CalculateSha(data).Response; // Optionally calculate SHA of the whole tree
            return Result<Tree>.Success(tree);
        }
        catch (Exception ex)
        {
            return Result<Tree>.Failure(GitServiceErrors.TreeParseError(ex.Message));
        }
    }

    private async Task<Result<Blob>> TryParseBlobAsync(byte[] data)
    {
        try
        {
            var headerEndIndex = Array.IndexOf(data, (byte)0);
            string header = Encoding.UTF8.GetString(data, 0, headerEndIndex);
            string[] headerParts = header.Split(' ');
            if (headerParts.Length != 2 || headerParts[0] != GitObjectType.Blob.Value ||
                !int.TryParse(headerParts[1], out int declaredLength) ||
                declaredLength != data.Length - headerEndIndex - 1)
            {
                return Result<Blob>.Failure(GitServiceErrors.ParseBlobHeaderError);
            }

            return Result<Blob>.Success(new Blob
            {
                Content = data[(headerEndIndex + 1)..],
                Sha = CalculateSha(data).Response
            });
        }
        catch (Exception ex)
        {
            return Result<Blob>.Failure(GitServiceErrors.BlobParseError(ex.Message));
        }
    }

    private async Task<Result<byte[]>> ReadGitObjectAsync(string sha)
    {
        return await ValidateAndRetrievePath(sha)
            .Tap(result => _logger.LogDebug($"Path Validate : {result}"))
            .TapError(result =>
                _logger.LogError(
                    $"Path Error : {JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })}"))
            .BindAsync(TryDecompressAsync)
            .TapAsync(result => _logger.LogDebug($"Decompression OK : {Encoding.UTF8.GetString(result.Response)}"))
            .TapErrorAsync(result =>
                _logger.LogError(
                    $"Decompression Error : {JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })}"));
    }

    private Result<string> ValidateAndRetrievePath(string sha)
    {
        return ValidateShaFormat(sha)
            .Bind(_ => ConstructPath(sha))
            .Bind(ValidateExist);
    }

    private async Task<Result<None>> TryWriteDataAsync(string path, string fileName, byte[] data)
    {
        return ResultExtensions.TryExecute(() =>
        {
            using var fileStream = new FileStream(Path.Combine(path, fileName), FileMode.CreateNew);
            using ZLibStream zLibStream = new ZLibStream(fileStream, CompressionMode.Compress);
            zLibStream.Write(data, 0, data.Length);
            _logger.LogDebug($"File Written to : {path}");
            return None.Value;
        }, ex => new List<Error>() { GitServiceErrors.WritingFileError(ex.Message) });
    }

    private async Task<Result<byte[]>> TryDecompressAsync(string path)
    {
        return ResultExtensions.TryExecute(() =>
        {
            using var fileStream = File.OpenRead(path);
            using Stream compressedStream = new ZLibStream(fileStream, CompressionMode.Decompress);
            using MemoryStream uncompressedStream = new();
            compressedStream.CopyTo(uncompressedStream);
            _logger.LogDebug($"File Decompressed");
            return uncompressedStream.ToArray();
        }, ex => new List<Error>() { GitServiceErrors.DecompressionError(ex.Message) });
    }

    private Result<string> CalculateSha(byte[] data)
    {
        var sha1 = SHA1.HashData(data);
        string sha = Convert.ToHexString(sha1).ToLower();
        _logger.LogDebug($"Generated sha: {sha}");
        return Result<string>.Success(sha);
    }

    private async Task<Result<byte[]>> GenerateBlobData(string path)
    {
        string content = File.ReadAllText(path);
        _logger.LogDebug(content);

        string header = $"{GitObjectType.Blob.Value} {content.Length}\0";
        _logger.LogDebug(header);

        return Result<byte[]>.Success(Encoding.UTF8.GetBytes(header + content));
    }

    private string DetermineType(string mode)
    {
        if (mode.StartsWith("100"))
        {
            return "blob";
        }
        else if (mode == "040000")
        {
            return "tree";
        }
        else
        {
            return "unknown"; // This handles any unexpected modes
        }
    }

    private Result<None> ValidateShaFormat(string sha)
    {
        Regex shaRegex = new Regex("^[a-fA-F0-9]{" + _shaLength + "}$");
        if (!shaRegex.IsMatch(sha))
            return Result<None>.Failure(GitServiceErrors.InvalidShaFormat);

        return Result<None>.Success(None.Value);
    }

    private Result<string> ConstructPath(string sha)
    {
        string path = Path.Combine(_pathToGitObjectFolder, sha[..2], sha[2..]);
        return Result<string>.Success(path);
    }

    private Result<string> ValidateExist(string path)
    {
        if (!File.Exists(path))
            return Result<string>.Failure(GitServiceErrors.NotFound);

        return Result<string>.Success(path);
    }
}

public static class GitServiceErrors
{
    public static readonly Error ParseBlobHeaderError =
        new Error("ParseBlobHeaderError", "Failed to parse blob header or length mismatch.");

    public static readonly Error ParseTreeError = new Error("ParseTreeError", "Error during the parsing of a tree");

    public static readonly Error InvalidShaFormat =
        new Error("InvalidShaFormat", "The SHA-1 hash must be exactly 40 characters long.");

    public static readonly Error NotFound = new Error("NotFound", "Not Found");
    public static readonly Error TypeInvalid = new Error("TypeInvalid", "The object is not a valid 'blob' type.");

    public static readonly Error LengthInvalid =
        new Error("LengthInvalid", "Length does not match the expected length.");

    public static readonly Error HeaderInvalid = new Error("HeaderInvalid", "Header is malformed.");

    public static Error DecompressionError(string message) =>
        new Error("DecompressionError", $"Failed to decompress: {message}");

    public static Error WritingFileError(string message) =>
        new Error("WritingFileError", $"Error during the writing process : {message}");

    public static Error BlobParseError(string message) =>
        new Error("BlobParseError", $"Failed to parse to blob : {message}");

    public static Error TreeParseError(string message) =>
        new Error("TreeParseError", $"Failed to parse to tree : {message}");
}

public class GitObjectType
{
    public static readonly GitObjectType Tree = new GitObjectType("tree");
    public static readonly GitObjectType Blob = new GitObjectType("blob");

    public string Value { get; }

    public GitObjectType(string value)
    {
        Value = value;
    }
}

public class Tree
{
    public List<TreeEntry> Entries { get; set; } = new List<TreeEntry>();
    public string Sha { get; set; } // Unique hash of the structure

    public class TreeEntry
    {
        public string Path { get; set; }
        public virtual string Mode { get; set; } // e.g., '100644' for regular file, '040000' for directory
        public virtual string Type { get; set; } // 'blob' or 'tree'
        public string Sha { get; set; } // SHA-1 hash of the referenced object
    }

    public class FileEntry : TreeEntry
    {
        public override string Mode { get => "100644"; }
        public override string Type { get => "blob"; }
    }
    public class DirectoryEntry : TreeEntry
    {
        public override string Mode { get => "040000"; }
        public override string Type { get => "tree"; }
    }
}

public class Blob
{
    public byte[] Content { get; set; }
    public string Sha { get; set; } // Unique hash of the Content
}