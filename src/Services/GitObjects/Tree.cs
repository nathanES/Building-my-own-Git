namespace codecrafters_git.Services.GitObjects;

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