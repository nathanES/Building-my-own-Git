using Cocona;
using System;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using codecrafters_git.ResultPattern;
using codecrafters_git.Utils;

namespace codecrafters_git.Commands;

public class GitCatFileCommand
{
    private readonly ICustomWriter _customWriter;
    private const int _blobShaLength = 40;
    private const string _blobType = "blob";
    public GitCatFileCommand(ICustomWriter customWriter)
    {
        _customWriter = customWriter;
    }

    [Command("cat-file", Description = "CatFileCommand")]
    public void CatFile([Option('p', Description = "BlobSha")] string blobSha)
    {
        var catFileTreatmentResult = PrettyPrintTreatment(blobSha);
        if(catFileTreatmentResult.IsFailure)
        {
            _customWriter.WriteLine(catFileTreatmentResult.Errors);
            return;
        }
    }

    private Result<None> PrettyPrintTreatment(string blobSha)
    {
        return ValidateBlobShaFormat(blobSha)
            .Bind(_ => ConstructBlobPath(blobSha))
            .Bind(ValidateBlobPresence)
            .Bind(blobPath => TryUncompressBlob(blobPath)
                    .Bind(uncompressedBlob => CreateContextAndPrint(blobPath, uncompressedBlob)));
        
        // var validatonResult = ValidateBlobShaFormat(blobSha)
        //     .Bind(_ => ConstructBlobPath(blobSha))
        //     .Bind(blobPath => ValidateBlobPresence(blobPath))  // Use the blobPath correctly
        //     .Bind(TryUncompressBlob(blobPath))
        //     .Bind(uncompressedBlob => ValidateAndPrintBlob(uncompressedBlob, blobSha));
        //
        // return validationResult;
        //
        //
        // var validationBlobShaFormatResult = ValidateBlobShaFormat(blobSha);
        // if (validationBlobShaFormatResult.IsFailure)
        //     return validationBlobShaFormatResult;
        //
        // var blobPath = ConstructBlobPath(blobSha);
        //
        // var validationBlobPresenceResult = ValidateBlobPresence(blobPath);
        // if (validationBlobPresenceResult.IsFailure)
        //     return validationBlobPresenceResult;
        //
        // var uncompressResult = TryUncompressBlob(blobPath);
        // if (uncompressResult.IsFailure)
        //     return Result<None>.Create(uncompressResult.Error);
        //
        // Memory<byte> uncompressedBlob = uncompressResult.Response;
        // int nullByteIndex = uncompressedBlob.Span.IndexOf((byte)0);
        // int spaceByteIndex = uncompressedBlob.Span.IndexOf((byte)' ');
        // string blobType = Encoding.UTF8.GetString(uncompressedBlob[0..spaceByteIndex].Span);
        //
        // var validationBlobTypeResult = ValidateBlobType(blobType);
        // if (validationBlobTypeResult.IsFailure)
        //     return validationBlobTypeResult;
        //
        // string blobContent = Encoding.UTF8.GetString(uncompressedBlob[(nullByteIndex + 1)..].Span);
        // string headerBlobLength = Encoding.UTF8.GetString(uncompressedBlob[(spaceByteIndex + 1)..nullByteIndex].Span);
        //
        // var validationBlobLengthResult = ValidateBlobLength(blobContent.Length, headerBlobLength);
        // if (validationBlobLengthResult.IsFailure)
        //     return validationBlobLengthResult;
        //
        // Console.Write(blobContent);
        // return Result<None>.Create(None.Value); 
    }
    private Result<None> CreateContextAndPrint(string blobPath, Memory<byte> uncompressedBlob)
    {
        var context = new BlobProcessingContext(blobPath, uncompressedBlob);
        return ValidateAndPrintBlob(context);
    }

    private Result<string> ConstructBlobPath(string blobSha)
    {
        string blobPath =  Path.Combine(PathFile.PATH_TO_GIT_OBJECTS_FOLDER, blobSha[..2], blobSha[2..]);
        return Result<string>.Create(blobPath);
    }

    private Result<None> ValidateBlobShaFormat(string blobSha)
    {
        if (blobSha?.Length != _blobShaLength)
            return Result<None>.Create(GitCatFileErrors.InvalidShaFormat);
        
        return Result<None>.Create(None.Value);
    }
    private Result<string> ValidateBlobPresence(string pathToBlobFile)
    {
        if (!File.Exists(pathToBlobFile))
            return Result<string>.Create(GitCatFileErrors.BlobNotFound);

        return Result<string>.Create(pathToBlobFile);
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
            return Result<None>.Create(GitCatFileErrors.BlobCompositionInvalid);
        
        if (actualBlobLength != blobLength)
            return Result<None>.Create(GitCatFileErrors.BlobLengthInvalid);
        
        return Result<None>.Create(None.Value); 
    }
    private Result<None> ValidateAndPrintBlob(BlobProcessingContext context)
    {
        int nullByteIndex = context.UncompressedBlob.Span.IndexOf((byte)0);
        int spaceByteIndex = context.UncompressedBlob.Span.IndexOf((byte)' ');

        string blobType = Encoding.UTF8.GetString(context.UncompressedBlob[..spaceByteIndex].Span);
        var validationBlobTypeResult = ValidateBlobType(blobType);
        if (validationBlobTypeResult.IsFailure)
            return validationBlobTypeResult;

        string blobContent = Encoding.UTF8.GetString(context.UncompressedBlob[(nullByteIndex + 1)..].Span);
        string headerBlobLength = Encoding.UTF8.GetString(context.UncompressedBlob[(spaceByteIndex + 1)..nullByteIndex].Span);

        var validationBlobLengthResult = ValidateBlobLength(blobContent.Length, headerBlobLength);
        if (validationBlobLengthResult.IsFailure)
            return validationBlobLengthResult;

        Console.Write(blobContent);
        return Result<None>.Create(None.Value);
    }
    private Result<Memory<byte>> TryUncompressBlob(string filePath)
    {
        try
        {
            using Stream compressedStream = new ZLibStream(File.OpenRead(filePath), CompressionMode.Decompress);
            using MemoryStream uncompressedStream = new();
            compressedStream.CopyTo(uncompressedStream);
            return Result<Memory<byte>>.Create(new Memory<byte>(uncompressedStream.GetBuffer())[..(int)uncompressedStream.Length]);
        }
        catch (Exception ex)
        {
            return Result<Memory<byte>>.Create(GitCatFileErrors.DecompressionFailed(ex.Message));
        }
    }
}

public static class GitCatFileErrors
{
    public static readonly Error InvalidShaFormat = new Error("InvalidShaFormat", "The SHA-1 hash must be 40 characters long.");
    public static readonly Error BlobNotFound = new Error("BlobNotFound", "The object with SHA-1 does not exist.");
    public static readonly Error BlobTypeInvalid = new Error("BlobTypeInvalid", "The blob don't have a type valid.");
    public static readonly Error BlobLengthInvalid = new Error("BlobLengthInvalid", "The blob don't have a length valid.");
    public static readonly Error BlobCompositionInvalid = new Error("BlobCompositionInvalid", "The blob don't have a good composition.");
    public static Error DecompressionFailed(string message) => new Error("DecompressionFailed", $"Failed to decompress the blob: {message}");
}
public class BlobProcessingContext
{
    public string BlobPath { get; }
    public Memory<byte> UncompressedBlob { get; }

    public BlobProcessingContext(string blobPath, Memory<byte> uncompressedBlob)
    {
        BlobPath = blobPath;
        UncompressedBlob = uncompressedBlob;
    }
}