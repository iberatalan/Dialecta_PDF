using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using PdfMaster.Models;
using PdfMaster.Services;

namespace PdfMaster.ViewModels
{
    public class CompressViewModel : ViewModelBase
    {
        private string _selectedFilePath = "";
        public string SelectedFilePath
        {
            get => _selectedFilePath;
            set
            {
                if (SetProperty(ref _selectedFilePath, value))
                {
                    OnSelectedFileChanged();
                }
            }
        }

        public string SelectedFileName => string.IsNullOrEmpty(SelectedFilePath) ? "Dosya Seçilmedi" : Path.GetFileName(SelectedFilePath);

        private string _fileSizeText = "";
        public string FileSizeText
        {
            get => _fileSizeText;
            set => SetProperty(ref _fileSizeText, value);
        }

        private CompressionPreset _selectedPreset = CompressionPreset.Medium;
        public CompressionPreset SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (SetProperty(ref _selectedPreset, value))
                {
                    OnPropertyChanged(nameof(IsLowSelected));
                    OnPropertyChanged(nameof(IsMediumSelected));
                    OnPropertyChanged(nameof(IsHighSelected));
                }
            }
        }

        public bool IsLowSelected
        {
            get => SelectedPreset == CompressionPreset.Low;
            set { if (value) SelectedPreset = CompressionPreset.Low; }
        }

        public bool IsMediumSelected
        {
            get => SelectedPreset == CompressionPreset.Medium;
            set { if (value) SelectedPreset = CompressionPreset.Medium; }
        }

        public bool IsHighSelected
        {
            get => SelectedPreset == CompressionPreset.High;
            set { if (value) SelectedPreset = CompressionPreset.High; }
        }

        private bool _compressStreams = true;
        public bool CompressStreams
        {
            get => _compressStreams;
            set => SetProperty(ref _compressStreams, value);
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

        public ICommand SelectFileCommand { get; }
        public ICommand CompressCommand { get; }
        public ICommand OpenOutputFileCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }
        public ICommand CloseFileCommand { get; }

        public CompressViewModel()
        {
            SelectFileCommand = new RelayCommand(SelectFile);
            CompressCommand = new RelayCommand(async () => await ExecuteCompressAsync(), () => !string.IsNullOrEmpty(SelectedFilePath) && !IsBusy);
            OpenOutputFileCommand = new RelayCommand(OpenOutputFile, () => !string.IsNullOrEmpty(LastOutputFile) && File.Exists(LastOutputFile));
            OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
            CloseFileCommand = new RelayCommand(CloseFile, () => !string.IsNullOrEmpty(SelectedFilePath) && !IsBusy);
        }

        private void CloseFile()
        {
            SelectedFilePath = "";
            FileSizeText = "";
            StatusMessage = "Dosya kapatıldı.";
            LastOutputFile = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public void SetFilePath(string path)
        {
            if (File.Exists(path) && path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                SelectedFilePath = path;
            }
        }

        private void SelectFile()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Sıkıştırılacak PDF Dosyasını Seçin",
                Filter = "PDF Belgeleri (*.pdf)|*.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                SelectedFilePath = dlg.FileName;
            }
        }

        private void OnSelectedFileChanged()
        {
            OnPropertyChanged(nameof(SelectedFileName));
            StatusMessage = "";
            LastOutputFile = null;

            if (string.IsNullOrEmpty(SelectedFilePath) || !File.Exists(SelectedFilePath))
            {
                FileSizeText = "";
                return;
            }

            var bytes = new FileInfo(SelectedFilePath).Length;
            FileSizeText = bytes < 1024 * 1024 
                ? $"{bytes / 1024.0:F1} KB" 
                : $"{bytes / (1024.0 * 1024.0):F2} MB";
        }

        private async Task ExecuteCompressAsync()
        {
            if (string.IsNullOrEmpty(SelectedFilePath)) return;

            IsBusy = true;
            StatusMessage = "PDF dosyası yerel olarak sıkıştırılıyor...";
            LastOutputFile = null;

            try
            {
                var options = new CompressionOptions
                {
                    Preset = SelectedPreset,
                    CompressStreams = CompressStreams
                };

                switch (SelectedPreset)
                {
                    case CompressionPreset.Low:
                        options.ImageQualityPercent = 80;
                        options.MaxImageDpi = 2000; // max dimension
                        break;
                    case CompressionPreset.Medium:
                        options.ImageQualityPercent = 50;
                        options.MaxImageDpi = 1500; // max dimension
                        break;
                    case CompressionPreset.High:
                        options.ImageQualityPercent = 30;
                        options.MaxImageDpi = 1000; // max dimension
                        break;
                }

                var result = await Task.Run(() => PdfCompressService.CompressPdf(SelectedFilePath, options));

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
                StatusMessage = $"Hata: {ex.Message}";
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
