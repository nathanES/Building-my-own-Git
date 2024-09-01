using Cocona;
using System;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using codecrafters_git.ResultPattern;
using codecrafters_git.Utils;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Commands;

public class GitCatFileCommand
{
    private readonly ILogger _logger;
    private const int _blobShaLength = 40;
    private const string _blobType = "blob";
    public GitCatFileCommand(ILogger logger)
    {
        _logger = logger;
    }

    [Command("cat-file", Description = "Git cat-file command")]
    public void GitCatFile([Option('p', Description = "BlobSha")] string blobSha)
    {
        var catFileTreatmentResult = PrettyPrintTreatment(blobSha);
        if(catFileTreatmentResult.IsFailure)
        {
            _logger.LogError(catFileTreatmentResult.Errors);
            return;
        }
    }

    private Result<None> PrettyPrintTreatment(string blobSha)
    {
        return ValidateAndRetrieveBlobPath(blobSha)
            .Bind(TryDecompressBlob)
            .Bind(ValidateAndExtractBlobContent)
            .Bind(PrintBlobContent);
    }
    private Result<string> ValidateAndRetrieveBlobPath(string blobSha)
    {
        return ValidateBlobShaFormat(blobSha)
            .Bind(_ => ConstructBlobPath(blobSha))
            .Bind(ValidateBlobExist);
    }
    private Result<None> ValidateBlobShaFormat(string blobSha)
    {
        if (blobSha?.Length != _blobShaLength)
            return Result<None>.Create(GitCatFileErrors.InvalidShaFormat);
        
        return Result<None>.Create(None.Value);
    }
    private Result<string> ConstructBlobPath(string blobSha)
    {
        string blobPath =  Path.Combine(FilePath.TO_GIT_OBJECTS_FOLDER, blobSha[..2], blobSha[2..]);
        return Result<string>.Create(blobPath);
    }
    private Result<string> ValidateBlobExist(string blobPath)
    {
        if (!File.Exists(blobPath))
            return Result<string>.Create(GitCatFileErrors.BlobNotFound);

        return Result<string>.Create(blobPath);
    }

    private Result<None> ValidateBlobType(string blobType)
    {
        if (blobType != _blobType)
            return Result<None>.Create(GitCatFileErrors.BlobTypeInvalid);
        
        return Result<None>.Create(None.Value); 
    }
    private Result<None> ValidateBlobLength(int actualBlobLength, string headerBlobLength )
    {
        if (!int.TryParse(headerBlobLength, out int blobLength))
            return Result<None>.Create(GitCatFileErrors.BlobHeaderInvalid);
        
        if (actualBlobLength != blobLength)
            return Result<None>.Create(GitCatFileErrors.BlobLengthInvalid);
        
        return Result<None>.Create(None.Value); 
    }
    private Result<Memory<byte>> TryDecompressBlob(string filePath)
    {
        return Result<Memory<byte>>.TryExecute(() =>
        {
            using Stream compressedStream = new ZLibStream(File.OpenRead(filePath), CompressionMode.Decompress);
            using MemoryStream uncompressedStream = new();
            compressedStream.CopyTo(uncompressedStream);
            return new Memory<byte>(uncompressedStream.GetBuffer())[..(int)uncompressedStream.Length];
        }, ex => GitCatFileErrors.DecompressionFailed(ex.Message));
    }
    private Result<string> ValidateAndExtractBlobContent(Memory<byte> uncompressedBlob)
    {
        var decomposeResult = DecomposeBlob(uncompressedBlob);
        if (decomposeResult.IsFailure)
            return Result<string>.Create(decomposeResult.Errors);
        
        var (blobType, blobContent, blobLength) = decomposeResult.Response;

        var validationBlobTypeResult = ValidateBlobType(blobType);
        if (validationBlobTypeResult.IsFailure)
            return Result<string>.Create(validationBlobTypeResult.Errors);

        var validationBlobLengthResult = ValidateBlobLength(blobContent.Length, blobLength);
        if (validationBlobLengthResult.IsFailure)
            return Result<string>.Create(validationBlobLengthResult.Errors);

        return Result<string>.Create(blobContent);
    }
    private Result<(string BlobType, string BlobContent, string BlobLength)> DecomposeBlob(Memory<byte> uncompressedBlob)
    {
        int nullByteIndex = uncompressedBlob.Span.IndexOf((byte)0);
        int spaceByteIndex = uncompressedBlob.Span.IndexOf((byte)' ');

        if (nullByteIndex < 0 || spaceByteIndex < 0 || spaceByteIndex >= nullByteIndex)
        {
            return Result<(string, string, string)>.Create(GitCatFileErrors.BlobHeaderInvalid);
        }

        string blobType = Encoding.UTF8.GetString(uncompressedBlob[..spaceByteIndex].Span);
        string blobLength = Encoding.UTF8.GetString(uncompressedBlob[(spaceByteIndex + 1)..nullByteIndex].Span);
        string blobContent = Encoding.UTF8.GetString(uncompressedBlob[(nullByteIndex + 1)..].Span);

        return Result<(string, string, string)>.Create((blobType, blobContent, blobLength));

    }
    private Result<None> PrintBlobContent(string blobContent)
    {
        Console.Write(blobContent);
        return Result<None>.Create(None.Value);
    } 

    // private Result<T> TryExecute<T>(Func<T> action, Func<Exception, Error> errorHandler)
    // {
    //     try
    //     {
    //         return Result<T>.Create(action());
    //     }
    //     catch (Exception ex)
    //     {
    //         return Result<T>.Create(errorHandler(ex));
    //     }
    // }
}

public static class GitCatFileErrors
{
    public static readonly Error InvalidShaFormat = new Error("InvalidShaFormat", "The SHA-1 hash must be exactly 40 characters long.");
    public static readonly Error BlobNotFound = new Error("BlobNotFound", "The object with the specified SHA-1 hash could not be found.");
    public static readonly Error BlobTypeInvalid = new Error("BlobTypeInvalid", "The object is not a valid 'blob' type.");
    public static readonly Error BlobLengthInvalid = new Error("BlobLengthInvalid", "The blob's content length does not match the expected length.");
    public static readonly Error BlobHeaderInvalid = new Error("BlobHeaderInvalid", "The blob's header is malformed.");
    public static Error DecompressionFailed(string message) => new Error("DecompressionFailed", $"Failed to decompress the blob: {message}");
}