using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using PdfMaster.Models;
using PdfMaster.Services;

namespace PdfMaster.ViewModels
{
    public class FileMergeItem : ViewModelBase
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public string FileSizeFormatted
        {
            get
            {
                try
                {
                    var bytes = new FileInfo(FilePath).Length;
                    if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
                    return $"{bytes / (1024.0 * 1024.0):F2} MB";
                }
                catch { return "0 KB"; }
            }
        }
        public int PageCount { get; set; }
    }

    public class MergeViewModel : ViewModelBase
    {
        public ObservableCollection<FileMergeItem> Files { get; set; } = new();

        private FileMergeItem? _selectedFile;
        public FileMergeItem? SelectedFile
        {
            get => _selectedFile;
            set => SetProperty(ref _selectedFile, value);
        }

        private string _customOutputName = "";
        public string CustomOutputName
        {
            get => _customOutputName;
            set => SetProperty(ref _customOutputName, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string? _lastOutputFile;
        public string? LastOutputFile
        {
            get => _lastOutputFile;
            set => SetProperty(ref _lastOutputFile, value);
        }

        private int _selectedSortIndex = 0;
        public int SelectedSortIndex
        {
            get => _selectedSortIndex;
            set
            {
                if (SetProperty(ref _selectedSortIndex, value))
                {
                    SortFiles(value);
                }
            }
        }
        
        private void SortFiles(int sortIndex)
        {
            if (Files.Count < 2 || sortIndex == 0) return;
            var list = Files.ToList();
            switch (sortIndex)
            {
                case 1: list = list.OrderBy(x => x.FileName).ToList(); break;
                case 2: list = list.OrderByDescending(x => x.FileName).ToList(); break;
                case 3: list = list.OrderBy(x => new FileInfo(x.FilePath).LastWriteTime).ToList(); break;
                case 4: list = list.OrderByDescending(x => new FileInfo(x.FilePath).LastWriteTime).ToList(); break;
            }
            Files.Clear();
            foreach (var item in list) Files.Add(item);
            
            // Trigger UI update without entering infinite loop
            _selectedSortIndex = 0;
            OnPropertyChanged(nameof(SelectedSortIndex));
        }

        public ICommand AddFilesCommand { get; }
        public ICommand RemoveFileCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand ClearFilesCommand { get; }
        public ICommand MergeCommand { get; }
        public ICommand OpenOutputFileCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }

        public MergeViewModel()
        {
            AddFilesCommand = new RelayCommand(AddFiles);
            RemoveFileCommand = new RelayCommand(RemoveFile, () => SelectedFile != null);
            MoveUpCommand = new RelayCommand(MoveUp, () => SelectedFile != null && Files.IndexOf(SelectedFile) > 0);
            MoveDownCommand = new RelayCommand(MoveDown, () => SelectedFile != null && Files.IndexOf(SelectedFile) < Files.Count - 1);
            ClearFilesCommand = new RelayCommand(() => 
            {
                Files.Clear();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });
            MergeCommand = new RelayCommand(async () => await ExecuteMergeAsync(), () => Files.Count >= 2 && !IsBusy);
            OpenOutputFileCommand = new RelayCommand(OpenOutputFile, () => !string.IsNullOrEmpty(LastOutputFile) && File.Exists(LastOutputFile));
            OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
        }

        public async void AddFilesFromPaths(string[] filePaths)
        {
            IsBusy = true;
            StatusMessage = "Dosyalar okunuyor, lütfen bekleyin...";
            
            try
            {
                var validFiles = await Task.Run(() =>
                {
                    var items = new System.Collections.Generic.List<FileMergeItem>();
                    foreach (var path in filePaths)
                    {
                        if (File.Exists(path) && path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            bool exists = false;
                            System.Windows.Application.Current.Dispatcher.Invoke(() => 
                            {
                                exists = Files.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase));
                            });
                            
                            if (!exists)
                            {
                                int pages = PdfRendererService.GetPageCount(path);
                                items.Add(new FileMergeItem { FilePath = path, PageCount = pages });
                            }
                        }
                    }
                    return items;
                });

                foreach (var item in validFiles)
                {
                    Files.Add(item);
                    await Task.Delay(1); // UI'ı kitlememek ve StackOverflow'u (dwrite.dll) önlemek için nefes payı
                }
                
                StatusMessage = $"{validFiles.Count} dosya başarıyla eklendi.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void AddFiles()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Birleştirilecek PDF Dosyalarını Seçin",
                Filter = "PDF Belgeleri (*.pdf)|*.pdf",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                AddFilesFromPaths(dlg.FileNames);
            }
        }

        private void RemoveFile()
        {
            if (SelectedFile != null)
            {
                Files.Remove(SelectedFile);
            }
        }

        private void MoveUp()
        {
            if (SelectedFile == null) return;
            int idx = Files.IndexOf(SelectedFile);
            if (idx > 0)
            {
                Files.Move(idx, idx - 1);
            }
        }

        private void MoveDown()
        {
            if (SelectedFile == null) return;
            int idx = Files.IndexOf(SelectedFile);
            if (idx < Files.Count - 1)
            {
                Files.Move(idx, idx + 1);
            }
        }

        private async Task ExecuteMergeAsync()
        {
            if (Files.Count < 2) return;

            IsBusy = true;
            StatusMessage = "PDF dosyaları güvenli bir şekilde birleştiriliyor...";
            LastOutputFile = null;

            try
            {
                var paths = Files.Select(f => f.FilePath).ToList();
                var result = await Task.Run(() => PdfMergeService.MergePdfs(paths, CustomOutputName));

                if (result.IsSuccess)
                {
                    LastOutputFile = result.OutputFilePath;
                    StatusMessage = result.Message;
                }
                else
                {
                    StatusMessage = $"Hata: {result.Message}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Beklenmeyen bir hata oluştu: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenOutputFile()
        {
            if (!string.IsNullOrEmpty(LastOutputFile) && File.Exists(LastOutputFile))
            {
                Process.Start(new ProcessStartInfo { FileName = LastOutputFile, UseShellExecute = true });
            }
        }

        private void OpenOutputFolder()
        {
            var folder = StorageManager.Instance.OutputsDirectory;
            if (Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
        }
    }
}
