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
    public class SignViewModel : ViewModelBase
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

        private string _signerName = Environment.UserName ?? "Ad Soyad";
        public string SignerName
        {
            get => _signerName;
            set => SetProperty(ref _signerName, value);
        }

        private string _reason = "Kişisel Belge Onayı ve Arşivleme";
        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        private string _location = "Yerel Cihaz (Çevrimdışı)";
        public string Location
        {
            get => _location;
            set => SetProperty(ref _location, value);
        }

        private SignType _signType = SignType.CryptoSealStamp;
        public SignType SignType
        {
            get => _signType;
            set => SetProperty(ref _signType, value);
        }

        private string? _signatureImagePath;
        public string? SignatureImagePath
        {
            get => _signatureImagePath;
            set => SetProperty(ref _signatureImagePath, value);
        }

        private double _posX = 50;
        public double PosX
        {
            get => _posX;
            set => SetProperty(ref _posX, value);
        }

        private double _posY = 50;
        public double PosY
        {
            get => _posY;
            set => SetProperty(ref _posY, value);
        }

        private int _pageIndex = 0;
        public int PageIndex
        {
            get => _pageIndex;
            set => SetProperty(ref _pageIndex, value);
        }

        private BitmapSource? _previewImage;
        public BitmapSource? PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
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

        private double _previewPageWidth = 595;
        public double PreviewPageWidth
        {
            get => _previewPageWidth;
            set
            {
                if (SetProperty(ref _previewPageWidth, value))
                    OnPropertyChanged(nameof(MaxPosX));
            }
        }

        private double _previewPageHeight = 842;
        public double PreviewPageHeight
        {
            get => _previewPageHeight;
            set
            {
                if (SetProperty(ref _previewPageHeight, value))
                    OnPropertyChanged(nameof(MaxPosY));
            }
        }

        public double MaxPosX => Math.Max(20, PreviewPageWidth - 260);
        public double MaxPosY => Math.Max(20, PreviewPageHeight - 85);

        public ICommand SelectFileCommand { get; }
        public ICommand SelectSignatureImageCommand { get; }
        public ICommand SignAndSealCommand { get; }
        public ICommand OpenOutputFileCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }
        public ICommand CloseFileCommand { get; }

        public SignViewModel()
        {
            SelectFileCommand = new RelayCommand(SelectFile);
            SelectSignatureImageCommand = new RelayCommand(SelectSignatureImage);
            SignAndSealCommand = new RelayCommand(async () => await ExecuteSignAndSealAsync(), () => !string.IsNullOrEmpty(SelectedFilePath) && !IsBusy);
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
                Title = "İmzalanacak / Mühürlenecek PDF Dosyasını Seçin",
                Filter = "PDF Belgeleri (*.pdf)|*.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                SelectedFilePath = dlg.FileName;
            }
        }

        private void SelectSignatureImage()
        {
            var dlg = new OpenFileDialog
            {
                Title = "İmza / Kaşe Görseli Seçin",
                Filter = "Görseller (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };
            if (dlg.ShowDialog() == true)
            {
                SignatureImagePath = dlg.FileName;
                SignType = SignType.Both;
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

            var dims = PdfRendererService.GetPageDimensions(SelectedFilePath, PageIndex);
            PreviewPageWidth = dims.Width;
            PreviewPageHeight = dims.Height;

            PreviewImage = PdfRendererService.RenderPageToBitmap(SelectedFilePath, PageIndex, 500);
        }

        private async Task ExecuteSignAndSealAsync()
        {
            if (string.IsNullOrEmpty(SelectedFilePath)) return;

            IsBusy = true;
            StatusMessage = "Belge yerel kriptografik mühürle mühürleniyor...";
            LastOutputFile = null;

            try
            {
                byte[]? sigBytes = null;
                if (!string.IsNullOrEmpty(SignatureImagePath) && File.Exists(SignatureImagePath))
                {
                    sigBytes = await File.ReadAllBytesAsync(SignatureImagePath);
                }

                var options = new SignSealOptions
                {
                    Type = SignType,
                    SignerName = SignerName,
                    Reason = Reason,
                    Location = Location,
                    PageIndex = PageIndex,
                    X = PosX,
                    Y = PosY,
                    Width = 260,
                    Height = 85,
                    SignatureImageBytes = sigBytes,
                    IncludeSha256HashInStamp = true,
                    IncludeTimestampInStamp = true
                };

                var result = await Task.Run(() => PdfSignService.SignAndSealPdf(SelectedFilePath, options));

                if (result.IsSuccess)
                {
                    LastOutputFile = result.OutputFilePath;
                    StatusMessage = result.Message;
                    if (!string.IsNullOrEmpty(result.OutputFilePath))
                    {
                        PreviewImage = PdfRendererService.RenderPageToBitmap(result.OutputFilePath, PageIndex, 500);
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
