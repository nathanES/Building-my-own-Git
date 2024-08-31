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
        Console.WriteLine(DateTime.Now);
        Console.WriteLine("BlobSha : " + blobSha);
        if (IsValidBlobSha(blobSha))
            PrettyPrint(blobSha);
    }

    private bool IsValidBlobSha(string blobSha) => blobSha?.Length == 40;

    private void PrettyPrint(string compressedBlob)
    {
        Console.WriteLine("Current Directory : " + Directory.GetCurrentDirectory());
        string path = PathFiles.PATH_TO_GIT_OBJECTS_FOLDER + compressedBlob[..2] + "/" + compressedBlob[2..];
        if (!File.Exists(path))
        {
            Console.WriteLine($"{nameof(compressedBlob)} does not exist");
            return;
        }
        
        using Stream compressedStream = new ZLibStream(File.OpenRead(path), CompressionMode.Decompress);
        using var uncompressedStream = new MemoryStream();
        compressedStream.CopyTo(uncompressedStream);
        Memory<byte> uncompressedStreamMemory = new Memory<byte>(uncompressedStream.GetBuffer())[..(int)uncompressedStream.Length];
        int nullByteIndex = uncompressedStreamMemory.Span.IndexOf((byte)0);
        int spaceByteIndex = uncompressedStreamMemory.Span.IndexOf((byte)' ');
        string blobType = Encoding.UTF8.GetString(uncompressedStreamMemory[0..spaceByteIndex].Span);
        string blobContent =Encoding.UTF8.GetString(uncompressedStreamMemory[(nullByteIndex+1)..].Span);
        if (int.TryParse(Encoding.UTF8.GetString(uncompressedStreamMemory[(spaceByteIndex + 1)..nullByteIndex].Span),
                out int blobLength)
            && blobLength != blobContent.Length)
        {
            Console.WriteLine("blobLength invalid");
            return;
        }
        
        Console.WriteLine(blobType);
        Console.WriteLine(blobLength);
        Console.Write(blobContent);
    }
}