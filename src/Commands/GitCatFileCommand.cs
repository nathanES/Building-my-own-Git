using Cocona;
using System;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace codecrafters_git.Commands;

public class GitCatFileCommand
{
    public GitCatFileCommand()
    {
    }

    [Command("cat-file", Description = "CatFileCommand")]
    public void CatFile([Option('p', Description = "BlobSha")] string blobSha)
    {
        if (IsValidBlobSha(blobSha))
            PrettyPrint(blobSha);
    }

    private bool IsValidBlobSha(string blobSha)
    {
        if (!(blobSha?.Length == 40))
        {
            Console.WriteLine($"{blobSha} Length Invalid");
            return false;
        }
        return true;
    }

    private void PrettyPrint(string blobSha)
    {
        string blobPath = PathFile.PATH_TO_GIT_OBJECTS_FOLDER + blobSha[..2] + "/" + blobSha[2..];
        if (!File.Exists(blobPath))
        {
            Console.WriteLine($"{nameof(blobSha)} does not exist");
            return;
        }

        Memory<byte> uncompressedBlob = UncompressBlob(blobPath);
        int nullByteIndex = uncompressedBlob.Span.IndexOf((byte)0);
        int spaceByteIndex = uncompressedBlob.Span.IndexOf((byte)' ');
        string blobType = Encoding.UTF8.GetString(uncompressedBlob[0..spaceByteIndex].Span);
        // if(blobType == "toto")
        Console.WriteLine("test");
        string blobContent = Encoding.UTF8.GetString(uncompressedBlob[(nullByteIndex + 1)..].Span);
        if (int.TryParse(Encoding.UTF8.GetString(uncompressedBlob[(spaceByteIndex + 1)..nullByteIndex].Span),
                out int blobLength)
            && blobLength != blobContent.Length)
        {
            Console.WriteLine( $"{nameof(blobSha)} Length Invalid");
            return;
        }

        Console.Write(blobContent);
    }

    private Memory<byte> UncompressBlob(string filePath)
    {
        using Stream compressedStream = new ZLibStream(File.OpenRead(filePath), CompressionMode.Decompress);
        using MemoryStream uncompressedStream = new();
        compressedStream.CopyTo(uncompressedStream);
        return new Memory<byte>(uncompressedStream.GetBuffer())[..(int)uncompressedStream.Length]; 
    }
}