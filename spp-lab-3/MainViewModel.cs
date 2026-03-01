using DirectoryScannerLib;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace DirectoryScannerApp
{
    public class MainViewModel : ViewModelBase
    {
        private CancellationTokenSource _cts;
        private bool _isScanning;
        private ObservableCollection<DirectoryNodeViewModel> _scannedData;

        public bool IsScanning
        {
            get => _isScanning;
            set { _isScanning = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DirectoryNodeViewModel> ScannedData
        {
            get => _scannedData;
            set { _scannedData = value; OnPropertyChanged(); }
        }

        public ICommand StartScanCommand { get; }
        public ICommand CancelCommand { get; }

        public MainViewModel()
        {
            StartScanCommand = new RelayCommand(ExecuteStartScan, _ => !IsScanning);
            CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsScanning);
        }

        private async void ExecuteStartScan(object parameter)
        {
            using (var dialog = new FolderBrowserDialog { Description = "Выберите папку для сканирования" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;

                string selectedPath = dialog.SelectedPath;
                ScannedData = null;
                IsScanning = true;
                _cts = new CancellationTokenSource();

                var scanner = new Scanner(maxThreads: 10);

                // запуск в фоновом потоке чтоб UI не зависал
                var resultNode = await Task.Run(() => scanner.Scan(selectedPath, _cts.Token));

                ScannedData = new ObservableCollection<DirectoryNodeViewModel>
                {
                    new DirectoryNodeViewModel(resultNode)
                };

                IsScanning = false;
            }
        }
    }
}
