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
    public class ImageFileModel
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
    }

    public class ImageToPdfViewModel : ViewModelBase
    {
        private const int MaxFileLimit = 3000;
        
        public ObservableCollection<ImageFileModel> Files { get; } = new();

        private bool _combineToSinglePdf = true;
        public bool CombineToSinglePdf
        {
            get => _combineToSinglePdf;
            set
            {
                if (SetProperty(ref _combineToSinglePdf, value))
                {
                    OnPropertyChanged(nameof(CreateSeparatePdfs));
                }
            }
        }

        public bool CreateSeparatePdfs
        {
            get => !_combineToSinglePdf;
            set { CombineToSinglePdf = !value; }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private int _progressPercent;
        public int ProgressPercent
        {
            get => _progressPercent;
            set => SetProperty(ref _progressPercent, value);
        }

        private string _statusMessage = "İşlem bekleniyor.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _lastOutputPath = string.Empty;

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
            
            // Sıralama menüsünü tekrar "Sırala..." pozisyonuna getir (Trigger the property change without triggering the sort)
            _selectedSortIndex = 0;
            OnPropertyChanged(nameof(SelectedSortIndex));
        }

        public ICommand AddFilesCommand { get; }
        public ICommand ClearFilesCommand { get; }
        public ICommand ConvertCommand { get; }
        public ICommand OpenOutputCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }

        public ICommand RemoveFileCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }

        public ImageToPdfViewModel()
        {
            AddFilesCommand = new RelayCommand(_ => AddFiles());
            ClearFilesCommand = new RelayCommand(_ =>
            {
                Files.Clear();
                StatusMessage = "Liste temizlendi.";
            }, _ => !IsBusy && Files.Count > 0);
            
            RemoveFileCommand = new RelayCommand(param =>
            {
                if (param is ImageFileModel model)
                {
                    Files.Remove(model);
                    StatusMessage = $"{model.FileName} listeden çıkarıldı.";
                }
            }, _ => !IsBusy);

            MoveUpCommand = new RelayCommand(param =>
            {
                if (param is ImageFileModel model)
                {
                    int index = Files.IndexOf(model);
                    if (index > 0)
                    {
                        Files.Move(index, index - 1);
                    }
                }
            }, _ => !IsBusy);

            MoveDownCommand = new RelayCommand(param =>
            {
                if (param is ImageFileModel model)
                {
                    int index = Files.IndexOf(model);
                    if (index >= 0 && index < Files.Count - 1)
                    {
                        Files.Move(index, index + 1);
                    }
                }
            }, _ => !IsBusy);

            ConvertCommand = new RelayCommand(async _ => await ConvertAsync(), _ => !IsBusy && Files.Count > 0);
            
            OpenOutputCommand = new RelayCommand(_ =>
            {
                if (File.Exists(_lastOutputPath))
                {
                    Process.Start(new ProcessStartInfo(_lastOutputPath) { UseShellExecute = true });
                }
            }, _ => CombineToSinglePdf && File.Exists(_lastOutputPath));

            OpenOutputFolderCommand = new RelayCommand(_ =>
            {
                string? dir = !string.IsNullOrEmpty(_lastOutputPath) && Directory.Exists(_lastOutputPath) 
                    ? _lastOutputPath 
                    : (!string.IsNullOrEmpty(_lastOutputPath) ? Path.GetDirectoryName(_lastOutputPath) : null);
                    
                if (dir != null && Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
                }
            }, _ => !string.IsNullOrEmpty(_lastOutputPath));
        }

        public void HandleDroppedFiles(string[] files)
        {
            if (IsBusy) return;

            int addedCount = 0;
            foreach (var file in files)
            {
                if (Files.Count >= MaxFileLimit) break;
                
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ImageToPdfService.SupportedExtensions.Contains(ext) && !Files.Any(f => f.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase)))
                {
                    Files.Add(new ImageFileModel { FilePath = file });
                    addedCount++;
                }
            }

            if (addedCount > 0)
                StatusMessage = $"{addedCount} görsel eklendi. Toplam: {Files.Count}";
            
            if (Files.Count >= MaxFileLimit)
                StatusMessage = $"Maksimum dosya limitine ({MaxFileLimit}) ulaşıldı.";
        }

        private void AddFiles()
        {
            if (Files.Count >= MaxFileLimit)
            {
                StatusMessage = $"Zaten maksimum limit olan {MaxFileLimit} dosyaya ulaştınız.";
                return;
            }

            var ofd = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Görsel Dosyaları (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Tüm Dosyalar (*.*)|*.*",
                Title = "Görsel Dosyalarını Seçin"
            };

            if (ofd.ShowDialog() == true)
            {
                HandleDroppedFiles(ofd.FileNames);
            }
        }

        private async Task ConvertAsync()
        {
            if (Files.Count == 0) return;

            string baseDir = StorageManager.Instance.BaseDirectory;
            IsBusy = true;
            ProgressPercent = 0;
            StatusMessage = "Dönüştürülüyor...";

            try
            {
                var filePaths = Files.Select(f => f.FilePath).ToList();

                if (CombineToSinglePdf)
                {
                    string outputName = $"ImagesToPdf_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    string outputPath = Path.Combine(baseDir, outputName);
                    
                    await ImageToPdfService.ConvertMultipleImagesToSinglePdf(filePaths, outputPath, percent => 
                    {
                        ProgressPercent = percent;
                    });

                    _lastOutputPath = outputPath;
                    
                    AuditLogger.LogEvent(new AuditEvent
                    {
                        Action = ActionType.ImageToPdf,
                        ActionDescription = $"Görsellerden Tek PDF ({Files.Count} adet) -> {outputName}",
                        TimestampUtc = DateTime.UtcNow,
                        OutputFileHash = SecurityAndHashService.ComputeSha256(outputPath),
                        OutputFileName = outputName
                    });
                    
                    StatusMessage = $"Başarıyla dönüştürüldü ve kaydedildi.";
                }
                else
                {
                    string outputDirName = $"ImageToPdf_{DateTime.Now:yyyyMMdd_HHmmss}";
                    string outputDirPath = Path.Combine(baseDir, outputDirName);
                    Directory.CreateDirectory(outputDirPath);

                    await ImageToPdfService.ConvertMultipleImagesToSeparatePdfs(filePaths, outputDirPath, percent => 
                    {
                        ProgressPercent = percent;
                    });

                    _lastOutputPath = outputDirPath;
                    
                    AuditLogger.LogEvent(new AuditEvent
                    {
                        Action = ActionType.ImageToPdf,
                        ActionDescription = $"Görsellerden Çoklu PDF ({Files.Count} adet) -> {outputDirName} klasörü",
                        TimestampUtc = DateTime.UtcNow,
                        OutputFileHash = "-",
                        OutputFileName = outputDirName
                    });
                    
                    StatusMessage = $"Tüm görseller başarıyla ayrı PDF'lere dönüştürüldü.";
                }
                
                Files.Clear();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Hata: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                ProgressPercent = 100;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
