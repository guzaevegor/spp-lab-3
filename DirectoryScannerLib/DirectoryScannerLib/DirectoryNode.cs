using System.Collections.Concurrent;

namespace DirectoryScannerLib
{
    public class DirectoryNode
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public double PercentOfParent { get; set; }

        public ConcurrentBag<DirectoryNode> Children { get; set; } = new ConcurrentBag<DirectoryNode>();
    }
}
