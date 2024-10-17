namespace codecrafters_git.Services;

public class Blob
{
    public byte[] Content { get; set; }
    public string Sha { get; set; } // Unique hash of the Content
}