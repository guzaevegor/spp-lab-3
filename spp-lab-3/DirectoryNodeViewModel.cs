using DirectoryScannerLib;
using System.Collections.ObjectModel;
using System.Linq;

namespace DirectoryScannerApp
{
    public class DirectoryNodeViewModel : ViewModelBase
    {
        private readonly DirectoryNode _node;

        public string Name => _node.Name;
        public long Size => _node.Size;
        public string DisplaySize => $"{Size:N0} байт";
        public string Percent => $"{_node.PercentOfParent:F2}%";
        public bool IsDirectory => _node.IsDirectory;
        public string IconPath => IsDirectory ? "📁 " : "📄 ";

        public ObservableCollection<DirectoryNodeViewModel> Children { get; }

        public DirectoryNodeViewModel(DirectoryNode node)
        {
            _node = node;

            var sortedChildren = node.Children
                .OrderByDescending(c => c.IsDirectory)
                .ThenBy(c => c.Name)
                .Select(c => new DirectoryNodeViewModel(c));

            Children = new ObservableCollection<DirectoryNodeViewModel>(sortedChildren);
        }
    }
}
