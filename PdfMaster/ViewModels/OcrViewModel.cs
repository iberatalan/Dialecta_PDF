using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PdfMaster.Services;

namespace PdfMaster.ViewModels
{
    public class OcrViewModel : ViewModelBase
    {
        private string _selectedFilePath = "";
        public string SelectedFilePath
        {
            get => _selectedFilePath;
            set
            {
                if (SetProperty(ref _selectedFilePath, value))
                {
                    OnPropertyChanged(nameof(SelectedFileName));
                    ExtractedText = "";
                    StatusMessage = "";
                }
            }
        }

        public string SelectedFileName => string.IsNullOrEmpty(SelectedFilePath) ? "Dosya Seçilmedi" : Path.GetFileName(SelectedFilePath);

        private string _selectedLanguage = "tur+eng";
        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set => SetProperty(ref _selectedLanguage, value);
        }

        private string _extractedText = "";
        public string ExtractedText
        {
            get => _extractedText;
            set
            {
                if (SetProperty(ref _extractedText, value))
                {
                    OnPropertyChanged(nameof(HasNoFile));
                    OnPropertyChanged(nameof(HasPreview));
                    OnPropertyChanged(nameof(HasText));
                }
            }
        }

        private int _progressPercent;
        public int ProgressPercent
        {
            get => _progressPercent;
            set => SetProperty(ref _progressPercent, value);
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

        private string? _lastTextFile;
        public string? LastTextFile
        {
            get => _lastTextFile;
            set => SetProperty(ref _lastTextFile, value);
        }

        private BitmapSource? _previewImage;
        public BitmapSource? PreviewImage
        {
            get => _previewImage;
            set
            {
                if (SetProperty(ref _previewImage, value))
                {
                    OnPropertyChanged(nameof(HasNoFile));
                    OnPropertyChanged(nameof(HasPreview));
                    OnPropertyChanged(nameof(HasText));
                }
            }
        }

        public bool HasNoFile => _previewImage == null && string.IsNullOrEmpty(_extractedText);
        public bool HasPreview => _previewImage != null && string.IsNullOrEmpty(_extractedText);
        public bool HasText => !string.IsNullOrEmpty(_extractedText);

        private CancellationTokenSource? _cts;

        public ICommand SelectFileCommand { get; }
        public ICommand StartOcrCommand { get; }
        public ICommand CancelOcrCommand { get; }
        public ICommand CopyTextCommand { get; }
        public ICommand OpenTextFileCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }
        public ICommand CloseFileCommand { get; }

        public OcrViewModel()
        {
            SelectFileCommand = new RelayCommand(SelectFile);
            StartOcrCommand = new RelayCommand(async () => await ExecuteOcrAsync(), () => !string.IsNullOrEmpty(SelectedFilePath) && !IsBusy);
            CancelOcrCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
            CopyTextCommand = new RelayCommand(CopyText, () => !string.IsNullOrEmpty(ExtractedText));
            OpenTextFileCommand = new RelayCommand(OpenTextFile, () => !string.IsNullOrEmpty(LastTextFile) && File.Exists(LastTextFile));
            OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
            CloseFileCommand = new RelayCommand(CloseFile, () => !string.IsNullOrEmpty(SelectedFilePath) && !IsBusy);
        }

        private void CloseFile()
        {
            SelectedFilePath = "";
            PreviewImage = null;
            ExtractedText = "";
            StatusMessage = "Dosya kapatıldı.";
            LastTextFile = null;
            ProgressPercent = 0;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public void SetFilePath(string path)
        {
            if (File.Exists(path) && path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                SelectedFilePath = path;
                PreviewImage = PdfRendererService.RenderPageToBitmap(SelectedFilePath, 0, 500);
            }
        }

        private void SelectFile()
        {
            var dlg = new OpenFileDialog
            {
                Title = "OCR Yapılacak PDF Dosyasını Seçin",
                Filter = "PDF Belgeleri (*.pdf)|*.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                SelectedFilePath = dlg.FileName;
                PreviewImage = PdfRendererService.RenderPageToBitmap(SelectedFilePath, 0, 500);
            }
        }

        private void CopyText()
        {
            if (!string.IsNullOrEmpty(ExtractedText))
            {
                Clipboard.SetText(ExtractedText);
                StatusMessage = "Metin panoya kopyalandı! 📋";
            }
        }

        private async Task ExecuteOcrAsync()
        {
            if (string.IsNullOrEmpty(SelectedFilePath)) return;

            IsBusy = true;
            ProgressPercent = 0;
            StatusMessage = "Yerel OCR motoru çalışıyor (Çevrimdışı)...";
            ExtractedText = "";
            LastTextFile = null;
            _cts = new CancellationTokenSource();

            var progress = new Progress<int>(p => ProgressPercent = p);

            try
            {
                var result = await Task.Run(() => PdfOcrService.PerformOcr(
                    SelectedFilePath,
                    SelectedLanguage,
                    progress,
                    _cts.Token));

                if (result.IsSuccess)
                {
                    ExtractedText = result.ExtractedText;
                    LastTextFile = result.TextFilePath;
                    StatusMessage = $"OCR tamamlandı! ({result.PageCount} sayfa, Güven: %{result.MeanConfidence * 100:F0})";
                }
                else
                {
                    StatusMessage = $"Hata: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Hata: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void OpenTextFile()
        {
            if (!string.IsNullOrEmpty(LastTextFile) && File.Exists(LastTextFile))
            {
                Process.Start(new ProcessStartInfo { FileName = LastTextFile, UseShellExecute = true });
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
