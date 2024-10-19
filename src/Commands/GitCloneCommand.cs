using System.IO.Compression;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Cocona;
using codecrafters_git.ResultPattern;
using codecrafters_git.Services;
using Microsoft.Extensions.Logging;

namespace codecrafters_git.Commands;

public class GitCloneCommand(ILogger logger, IGitService gitService)
{
    private readonly ILogger _logger = logger;
    private readonly IGitService _gitService = gitService;

    [Command("clone", Description = "Git clone command")]
    public async Task
        Clone([Argument(Description = "repoUrl")] string repoUrl, [Argument(Description = "TargetDirectory")]string targetDirectory)
    {
        // Step 1: Fetch the remote refs
        var fetchRemoteRefsResult = await FetchRemoteRefs(repoUrl);
        var remoteRefsResult = await ParseFetchRemoteRefs(fetchRemoteRefsResult.Response);

        // Step 2: Select the ref/branch you want to clone (e.g., refs/heads/master)
        GitRef branchToClone = remoteRefsResult.Response.FirstOrDefault(r => r.RefName == "refs/heads/master"|| r.RefName =="refs/heads/main")!;
        if (branchToClone == null)
        {
            throw new Exception("Branch not found");
        }

        // Step 3: Request the packfile for the selected branch (commit hash)
        string packRequest = BuildPackRequest(branchToClone.CommitSha);
        byte[] packFileResponse = await RequestPackFile(repoUrl, packRequest);

        // Step 4: Unpack the packfile (decompress, parse objects)
        List<GitObject> gitObjects = UnpackPackFile(packFileResponse);

        // Step 5: Write objects to .git/objects/
        foreach (var obj in gitObjects)
        {
            WriteObjectToGitDirectory(obj, targetDirectory);
        }

        // Step 6: Update local refs (refs/heads/master and HEAD)
        UpdateLocalRefs(branchToClone, targetDirectory);

        // Step 7: Optional - Checkout files into the working directory
        CheckoutWorkingDirectory(branchToClone, targetDirectory);
    }
    private string BuildPackRequest(string commitSha)
    {
        // The 'want' line that specifies the commit hash we want
        string requestLine = $"want {commitSha} multi_ack thin-pack ofs-delta\n";
    
        // Calculate the total packet length
        int length = requestLine.Length + 4;  // 4 bytes for the length prefix itself

        // Convert the length to a 4-character hexadecimal string (length prefix)
        string lengthPrefix = length.ToString("X4").ToLower();  // Convert to lowercase hex

        // Return the full request with the length prefix and '0000' to end the request
        return $"{lengthPrefix}{requestLine}0000";
    }

    public async Task<Result<string>> FetchRemoteRefs(string repoUrl)
    {
        string serviceUrl = $"{repoUrl}/info/refs?service=git-upload-pack";
        using (HttpClient client = new HttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(serviceUrl);
                if (!response.IsSuccessStatusCode)
                    return Result<string>.Failure(
                        GitCloneErrors.FetchRemoteError($"HttpStatus : {Enum.GetName(response.StatusCode)}"));

                return Result<string>.Success(await response.Content.ReadAsStringAsync());
            }
            catch (Exception e)
            {
                return Result<string>.Failure(GitCloneErrors.FetchRemoteError(e.Message));
            }
        }
    }

    public async Task<byte[]> RequestPackFile(string repoUrl, string packRequest)
    {
        using HttpClient httpClient = new HttpClient();

        // Prepare the HTTP request to ask for the packfile
        string requestUrl = repoUrl + "/git-upload-pack";
        httpClient.DefaultRequestHeaders.Add("User-Agent", "git/1.0");
        httpClient.DefaultRequestHeaders.Add("Accept", "application/x-git-upload-pack-result");
        
        // Create the content of the request in the form of a string with the correct headers
        var requestBody = new StringContent(packRequest, Encoding.UTF8, "application/x-git-upload-pack-request");

        // Send the POST request
        HttpResponseMessage response = await httpClient.PostAsync(requestUrl, requestBody);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to fetch packfile: {response.StatusCode}, {response.ReasonPhrase}");
        }
        

        // Retrieve the packfile as a byte array
        byte[] packFileData = await response.Content.ReadAsByteArrayAsync();

        return packFileData;
    }


    public async Task<Result<List<GitRef>>> ParseFetchRemoteRefs(string response)
    {
        try
        {
            List<GitRef> refs = new List<GitRef>();
            Dictionary<string, bool> capabilities = new Dictionary<string, bool>();

            // Split the response into lines
            string[] lines = response.Split("\n");

            foreach (string line in lines)
            {
                if (line == "0000") break; // End of the ref list

                // Skip the service announcement
                if (line.Contains("service=")) continue;

                // Remove the first 4 characters (length prefix)
                string content = line.Substring(4);

                if (content.Contains("HEAD")) // Line with HEAD and capabilities
                {
                    var parts = content.Split('\0'); // Split at null byte
                    string refAndHash = parts[0]; // Contains SHA-1 and HEAD

                    var refParts = refAndHash.Split(' ');
                    string commitSha = refParts[0]; // SHA-1 commit hash
                    string refName = refParts[1]; // Reference name (HEAD)

                    // Parse capabilities after \0
                    if (parts.Length > 1)
                    {
                        var capabilityList = parts[1].Split(' ');
                        foreach (var capability in capabilityList)
                        {
                            capabilities[capability] = true;
                        }
                    }

                    refs.Add(new GitRef
                    {
                        RefName = refName,
                        CommitSha = commitSha,
                        IsHEAD = true,
                        Capabilities = capabilities
                    });
                }
                else
                {
                    // Parsing other refs like 'refs/heads/master'
                    var refParts = content.Split(' ');
                    string commitSha = refParts[0];
                    string refName = refParts[1];

                    refs.Add(new GitRef
                    {
                        RefName = refName,
                        CommitSha = commitSha,
                        IsHEAD = false,
                        Capabilities = null
                    });
                }
            }

            return Result<List<GitRef>>.Success(refs);
        }
        catch (Exception e)
        {
            return Result<List<GitRef>>.Failure(GitCloneErrors.ParseFetchRemoteRefsError(e.Message));
        }
    }

    public void WriteObjectToGitDirectory(GitObject gitObject, string targetDirectory)
    {
        // Compute the path using the first two characters of the SHA
        string objectDir = Path.Combine(targetDirectory, ".git", "objects", gitObject.Sha.Substring(0, 2));
        string objectPath = Path.Combine(objectDir, gitObject.Sha.Substring(2));

        // Create the directory if it doesn't exist
        if (!Directory.Exists(objectDir))
        {
            Directory.CreateDirectory(objectDir);
        }

        // Compress the object content using zlib
        byte[] compressedData;
        using (var memoryStream = new MemoryStream())
        {
            using (var compressionStream = new ZLibStream(memoryStream, CompressionMode.Compress))
            {
                compressionStream.Write(gitObject.Content, 0, gitObject.Content.Length);
            }

            compressedData = memoryStream.ToArray();
        }

        // Write the compressed data to the file
        File.WriteAllBytes(objectPath, compressedData);
    }

    public void UpdateLocalRefs(GitRef branchToClone, string targetDirectory)
    {
        // Update refs/heads/master
        string refsDir = Path.Combine(targetDirectory, ".git", "refs", "heads");
        if (!Directory.Exists(refsDir))
        {
            Directory.CreateDirectory(refsDir);
        }

        string refPath = Path.Combine(refsDir, "master");
        File.WriteAllText(refPath, branchToClone.CommitSha);

        // Update HEAD to point to refs/heads/master
        string headPath = Path.Combine(targetDirectory, ".git", "HEAD");
        File.WriteAllText(headPath, "ref: refs/heads/master\n");
    }

    public void CheckoutWorkingDirectory(GitRef branchToClone, string targetDirectory)
    {
        // Get the SHA of the latest commit
        string commitSha = branchToClone.CommitSha;

        // Get the commit object (which should have already been fetched)
        GitObject commitObject = GetGitObject(commitSha, targetDirectory);

        if (commitObject == null || commitObject.ObjectType != "commit")
        {
            throw new Exception("Commit object not found or invalid.");
        }

        // Parse the commit to find the tree it references
        string treeSha = ParseTreeShaFromCommit(commitObject.Content);

        // Checkout the tree
        CheckoutTree(treeSha, targetDirectory, targetDirectory);
    }

    private void CheckoutTree(string treeSha, string rootDirectory, string targetDirectory)
    {
        GitObject treeObject = GetGitObject(treeSha, rootDirectory);

        if (treeObject == null || treeObject.ObjectType != "tree")
        {
            throw new Exception("Tree object not found or invalid.");
        }

        // Parse the tree object and create files and directories in the working directory
        List<TreeEntry> entries = ParseTree(treeObject.Content);
        foreach (var entry in entries)
        {
            string entryPath = Path.Combine(targetDirectory, entry.Path);

            if (entry.ObjectType == "blob")
            {
                // Get the blob object and write the file
                GitObject blobObject = GetGitObject(entry.Sha, rootDirectory);
                File.WriteAllBytes(entryPath, blobObject.Content);
            }
            else if (entry.ObjectType == "tree")
            {
                // Create a subdirectory and recursively checkout the tree
                if (!Directory.Exists(entryPath))
                {
                    Directory.CreateDirectory(entryPath);
                }

                CheckoutTree(entry.Sha, rootDirectory, entryPath);
            }
        }
    }
    private string ParseTreeShaFromCommit(byte[] commitContent)
    {
        // Example commit format: "tree <treeSha>\nparent <parentSha>\n..."
        string commitText = Encoding.UTF8.GetString(commitContent);
        string[] lines = commitText.Split('\n');
        foreach (string line in lines)
        {
            if (line.StartsWith("tree "))
            {
                return line.Substring(5).Trim();
            }
        }

        throw new Exception("Tree SHA not found in commit.");
    }
    private GitObject GetGitObject(string sha, string targetDirectory)
    {
        string objectDir = Path.Combine(targetDirectory, ".git", "objects", sha.Substring(0, 2));
        string objectPath = Path.Combine(objectDir, sha.Substring(2));

        if (!File.Exists(objectPath))
        {
            return null;
        }

        // Read and decompress the object
        byte[] compressedData = File.ReadAllBytes(objectPath);
        byte[] decompressedData;
        using (var memoryStream = new MemoryStream(compressedData))
        using (var decompressionStream = new ZLibStream(memoryStream, CompressionMode.Decompress))
        using (var outputStream = new MemoryStream())
        {
            decompressionStream.CopyTo(outputStream);
            decompressedData = outputStream.ToArray();
        }

        // Parse the object type and content
        int nullIndex = Array.IndexOf(decompressedData, (byte)0);
        string header = Encoding.UTF8.GetString(decompressedData, 0, nullIndex);
        string[] headerParts = header.Split(' ');

        string objectType = headerParts[0];
        byte[] content = decompressedData[(nullIndex + 1)..];
        return new GitObject(objectType, content, sha);
    }
    public List<TreeEntry> ParseTree(byte[] treeContent)
    {
        List<TreeEntry> entries = new List<TreeEntry>();

        int index = 0;
        while (index < treeContent.Length)
        {
            // Read the file mode (permissions) until we encounter a space character
            int modeEnd = Array.IndexOf(treeContent, (byte)' ', index);
            if (modeEnd == -1) throw new Exception("Invalid tree object format (mode not found).");
            string mode = Encoding.UTF8.GetString(treeContent, index, modeEnd - index);
            index = modeEnd + 1;

            // Read the file or directory name until we encounter a null byte
            int pathEnd = Array.IndexOf(treeContent, (byte)0, index);
            if (pathEnd == -1) throw new Exception("Invalid tree object format (path not found).");
            string path = Encoding.UTF8.GetString(treeContent, index, pathEnd - index);
            index = pathEnd + 1;

            // Read the next 20 bytes which represent the SHA-1 hash of the referenced object
            byte[] shaBytes = treeContent.Skip(index).Take(20).ToArray();
            string sha = BitConverter.ToString(shaBytes).Replace("-", "").ToLower();
            index += 20;

            // Determine the object type (either "blob" or "tree") based on the mode
            string objectType = mode.StartsWith("100") ? "blob" : mode == "40000" ? "tree" : "unknown";

            // Create a new TreeEntry and add it to the list
            entries.Add(new TreeEntry
            {
                Mode = mode,
                Path = path,
                Sha = sha,
                ObjectType = objectType
            });
        }

        return entries;
    }
public List<GitObject> UnpackPackFile(byte[] packFileData)
{
    List<GitObject> gitObjects = new List<GitObject>();
    using (var memoryStream = new MemoryStream(packFileData))
    using (var binaryReader = new BinaryReader(memoryStream))
    {
        // Step 1: Verify the packfile signature "PACK"
        byte[] signature = binaryReader.ReadBytes(4);
        string signatureStr = Encoding.ASCII.GetString(signature);
        if (signatureStr != "PACK")
        {
            throw new Exception("Invalid packfile signature.");
        }

        // Step 2: Read the version number (4 bytes, network byte order)
        int version = ReadNetworkOrderInt32(binaryReader);
        if (version != 2 && version != 3)
        {
            throw new Exception($"Unsupported packfile version: {version}");
        }

        // Step 3: Read the number of objects (4 bytes, network byte order)
        int objectCount = ReadNetworkOrderInt32(binaryReader);

        // Step 4: Process each object entry in the packfile
        for (int i = 0; i < objectCount; i++)
        {
            // Read the object type and size (n-byte variable length encoding)
            (string objectType, int objectSize) = ReadObjectTypeAndSize(binaryReader);

            // Read the compressed object data
            byte[] compressedData = binaryReader.ReadBytes(objectSize);

            // Decompress the object data
            byte[] decompressedData;
            using (var compressedStream = new MemoryStream(compressedData))
            using (var decompressionStream = new ZLibStream(compressedStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                decompressionStream.CopyTo(outputStream);
                decompressedData = outputStream.ToArray();
            }

            // Create a GitObject and add it to the list
            string sha = ComputeSHA1ForObject(objectType, decompressedData);
            gitObjects.Add(new GitObject(objectType, decompressedData, sha));
        }
    }

    return gitObjects;
}

// Helper method to read a 4-byte integer in network byte order (big-endian)
private int ReadNetworkOrderInt32(BinaryReader binaryReader)
{
    byte[] bytes = binaryReader.ReadBytes(4);
    if (BitConverter.IsLittleEndian)
    {
        Array.Reverse(bytes);
    }
    return BitConverter.ToInt32(bytes, 0);
}

// Helper method to read object type and size from the packfile
private (string objectType, int size) ReadObjectTypeAndSize(BinaryReader binaryReader)
{
    byte firstByte = binaryReader.ReadByte();
    int type = (firstByte >> 4) & 0b111; // Extract 3-bit type from the first byte
    int size = firstByte & 0b1111;       // Extract the first part of the size (4 bits)

    // If the MSB is set, there are more size bytes
    int shift = 4;
    while ((firstByte & 0b10000000) != 0)
    {
        firstByte = binaryReader.ReadByte();
        size |= (firstByte & 0b01111111) << shift;
        shift += 7;
    }

    string objectType = type switch
    {
        1 => "commit",
        2 => "tree",
        3 => "blob",
        4 => "tag",
        6 => "ofs_delta",
        7 => "ref_delta",
        _ => throw new Exception("Unknown object type")
    };

    return (objectType, size);
}

// Helper method to compute the SHA1 for a Git object
private string ComputeSHA1ForObject(string objectType, byte[] data)
{
    // Prepare the Git object header: "<type> <size>\0"
    string header = $"{objectType} {data.Length}\0";
    byte[] headerBytes = Encoding.UTF8.GetBytes(header);

    // Concatenate the header and the object data
    byte[] fullData = new byte[headerBytes.Length + data.Length];
    Array.Copy(headerBytes, 0, fullData, 0, headerBytes.Length);
    Array.Copy(data, 0, fullData, headerBytes.Length, data.Length);

    // Compute the SHA-1 hash
    using (var sha1 = SHA1.Create())
    {
        byte[] hashBytes = sha1.ComputeHash(fullData);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }
}
 

}

public class GitRef
{
    public string RefName { get; set; }
    public string CommitSha { get; set; }
    public bool IsHEAD { get; set; }
    public Dictionary<string, bool> Capabilities { get; set; }
}

public class GitObject
{
    public string ObjectType { get; set; } // Commit, Tree, Blob, etc.
    public byte[] Content { get; set; } // The actual object data
    public string Sha { get; set; } // The SHA-1 hash of the object

    public GitObject(string objectType, byte[] content, string sha)
    {
        ObjectType = objectType;
        Content = content;
        Sha = sha;
    }
}
public class TreeEntry
{
    public string Mode { get; set; }    // The file mode (e.g., "100644" for a regular file)
    public string Path { get; set; }    // The name of the file or directory
    public string Sha { get; set; }     // The SHA-1 hash of the referenced Git object (blob or tree)
    public string ObjectType { get; set; } // The type of object, usually "blob" or "tree"
}


public static class GitCloneErrors
{
    public static Error FetchRemoteError(string message) =>
        new Error("FetchRemoteRefsError", $"Failled to fetch remote refs: {message}");

    public static Error ParseFetchRemoteRefsError(string message) =>
        new Error("ParseFetchRemoteRefsError", $"Failled to parse the fetched remote refs: {message}");
}