using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PdfMaster.Models;
using PdfMaster.Services;

namespace PdfMaster.ViewModels
{
    public class WatermarkViewModel : ViewModelBase
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

        private WatermarkType _watermarkType = WatermarkType.Text;
        public WatermarkType WatermarkType
        {
            get => _watermarkType;
            set => SetProperty(ref _watermarkType, value);
        }

        private string _watermarkText = "GİZLİ / KİŞİSEL";
        public string WatermarkText
        {
            get => _watermarkText;
            set => SetProperty(ref _watermarkText, value);
        }

        private double _fontSize = 42;
        public double FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        private double _opacity = 0.35;
        public double Opacity
        {
            get => _opacity;
            set => SetProperty(ref _opacity, value);
        }

        private double _rotationAngle = 45;
        public double RotationAngle
        {
            get => _rotationAngle;
            set => SetProperty(ref _rotationAngle, value);
        }

        private string _colorHex = "#FF0000";
        public string ColorHex
        {
            get => _colorHex;
            set => SetProperty(ref _colorHex, value);
        }

        private WatermarkPlacement _placement = WatermarkPlacement.Diagonal;
        public WatermarkPlacement Placement
        {
            get => _placement;
            set => SetProperty(ref _placement, value);
        }

        private TargetPagesMode _targetPages = TargetPagesMode.AllPages;
        public TargetPagesMode TargetPages
        {
            get => _targetPages;
            set => SetProperty(ref _targetPages, value);
        }

        private string _customPageRange = "";
        public string CustomPageRange
        {
            get => _customPageRange;
            set => SetProperty(ref _customPageRange, value);
        }

        private string? _watermarkImagePath;
        public string? WatermarkImagePath
        {
            get => _watermarkImagePath;
            set => SetProperty(ref _watermarkImagePath, value);
        }

        private BitmapSource? _previewImage;
        public BitmapSource? PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
        }

        private double _previewPageWidth = 595;
        public double PreviewPageWidth
        {
            get => _previewPageWidth;
            set => SetProperty(ref _previewPageWidth, value);
        }

        private double _previewPageHeight = 842;
        public double PreviewPageHeight
        {
            get => _previewPageHeight;
            set => SetProperty(ref _previewPageHeight, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private bool _isIndeterminate = true;
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetProperty(ref _isIndeterminate, value);
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
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
        public ICommand SelectImageCommand { get; }
        public ICommand ApplyWatermarkCommand { get; }
        public ICommand OpenOutputFileCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }
        public ICommand CloseFileCommand { get; }

        public WatermarkViewModel()
        {
            SelectFileCommand = new RelayCommand(SelectFile);
            SelectImageCommand = new RelayCommand(SelectImage);
            ApplyWatermarkCommand = new RelayCommand(async () => await ExecuteApplyWatermarkAsync(), () => !string.IsNullOrEmpty(SelectedFilePath) && !IsBusy);
            OpenOutputFileCommand = new RelayCommand(OpenOutputFile, () => !string.IsNullOrEmpty(LastOutputFile) && File.Exists(LastOutputFile));
            OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
            CloseFileCommand = new RelayCommand(CloseFile, () => !string.IsNullOrEmpty(SelectedFilePath) && !IsBusy);
        }

        private void CloseFile()
        {
            SelectedFilePath = "";
            PreviewImage = null;
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
                Title = "Filigran Eklenecek PDF Dosyasını Seçin",
                Filter = "PDF Belgeleri (*.pdf)|*.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                SelectedFilePath = dlg.FileName;
            }
        }

        private void SelectImage()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Filigran Görseli Seçin",
                Filter = "Görseller (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };
            if (dlg.ShowDialog() == true)
            {
                WatermarkImagePath = dlg.FileName;
            }
        }

        private void OnSelectedFileChanged()
        {
            OnPropertyChanged(nameof(SelectedFileName));
            StatusMessage = "";
            LastOutputFile = null;

            if (string.IsNullOrEmpty(SelectedFilePath) || !File.Exists(SelectedFilePath))
            {
                PreviewImage = null;
                return;
            }

            var dims = PdfRendererService.GetPageDimensions(SelectedFilePath, 0);
            PreviewPageWidth = dims.Width;
            PreviewPageHeight = dims.Height;

            PreviewImage = PdfRendererService.RenderPageToBitmap(SelectedFilePath, 0, 500);
        }

        private async Task ExecuteApplyWatermarkAsync()
        {
            if (string.IsNullOrEmpty(SelectedFilePath)) return;

            IsBusy = true;
            StatusMessage = "Filigran uygulanıyor...";
            LastOutputFile = null;

            try
            {
                var options = new WatermarkOptions
                {
                    Type = WatermarkType,
                    Text = WatermarkText,
                    FontSize = FontSize,
                    Opacity = Opacity,
                    RotationAngle = RotationAngle,
                    ColorHex = ColorHex,
                    Placement = Placement,
                    TargetPages = TargetPages,
                    CustomPageRange = CustomPageRange,
                    ImagePath = WatermarkImagePath
                };

                IsIndeterminate = false;
                ProgressValue = 0;
                var progress = new Progress<double>(p => 
                {
                    ProgressValue = p;
                    StatusMessage = $"Filigran uygulanıyor... %{p:F1}";
                });

                var result = await Task.Run(() => PdfWatermarkService.ApplyWatermark(SelectedFilePath, options, null, progress));

                if (result.IsSuccess)
                {
                    LastOutputFile = result.OutputFilePath;
                    StatusMessage = result.Message;
                    if (!string.IsNullOrEmpty(result.OutputFilePath))
                    {
                        PreviewImage = PdfRendererService.RenderPageToBitmap(result.OutputFilePath, 0, 500);
                    }
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
                IsIndeterminate = true;
                ProgressValue = 0;
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
