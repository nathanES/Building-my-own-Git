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
        string filePath = PathFile.PATH_TO_GIT_OBJECTS_FOLDER + blobSha[..2] + "/" + blobSha[2..];
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"{nameof(blobSha)} does not exist");
            return;
        }

        Memory<byte> uncompressedStreamMemory = UncompressFile(filePath);
        int nullByteIndex = uncompressedStreamMemory.Span.IndexOf((byte)0);
        int spaceByteIndex = uncompressedStreamMemory.Span.IndexOf((byte)' ');
        string blobType = Encoding.UTF8.GetString(uncompressedStreamMemory[0..spaceByteIndex].Span);
        string blobContent = Encoding.UTF8.GetString(uncompressedStreamMemory[(nullByteIndex + 1)..].Span);
        if (int.TryParse(Encoding.UTF8.GetString(uncompressedStreamMemory[(spaceByteIndex + 1)..nullByteIndex].Span),
                out int blobLength)
            && blobLength != blobContent.Length)
        {
            Console.WriteLine( $"{nameof(blobSha)} Length Invalid");
            return;
        }

        Console.Write(blobContent);
    }

    private Memory<byte> UncompressFile(string filePath)
    {
        using Stream compressedStream = new ZLibStream(File.OpenRead(filePath), CompressionMode.Decompress);
        using MemoryStream uncompressedStream = new();
        compressedStream.CopyTo(uncompressedStream);
        return new Memory<byte>(uncompressedStream.GetBuffer())[..(int)uncompressedStream.Length]; 
    }
}