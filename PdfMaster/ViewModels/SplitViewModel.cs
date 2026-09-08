using System;
using System.Collections.ObjectModel;
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
    public class SplitViewModel : ViewModelBase
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

        private int _pageCount;
        public int PageCount
        {
            get => _pageCount;
            set => SetProperty(ref _pageCount, value);
        }

        private SplitMode _selectedMode = SplitMode.ExtractAllPages;
        public SplitMode SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (SetProperty(ref _selectedMode, value))
                {
                    OnPropertyChanged(nameof(IsExtractAllSelected));
                    OnPropertyChanged(nameof(IsPageRangesSelected));
                    OnPropertyChanged(nameof(IsEveryNPagesSelected));
                }
            }
        }

        public bool IsExtractAllSelected
        {
            get => SelectedMode == SplitMode.ExtractAllPages;
            set { if (value) SelectedMode = SplitMode.ExtractAllPages; }
        }

        public bool IsPageRangesSelected
        {
            get => SelectedMode == SplitMode.PageRanges;
            set { if (value) SelectedMode = SplitMode.PageRanges; }
        }

        public bool IsEveryNPagesSelected
        {
            get => SelectedMode == SplitMode.EveryNPages;
            set { if (value) SelectedMode = SplitMode.EveryNPages; }
        }

        private string _rangeText = "1-2, 3-4";
        public string RangeText
        {
            get => _rangeText;
            set => SetProperty(ref _rangeText, value);
        }

        private int _everyNPages = 2;
        public int EveryNPages
        {
            get => _everyNPages;
            set => SetProperty(ref _everyNPages, value);
        }

        private BitmapSource? _previewImage;
        public BitmapSource? PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
        }

        private int _previewPageIndex = 0;
        public int PreviewPageIndex
        {
            get => _previewPageIndex;
            set
            {
                if (SetProperty(ref _previewPageIndex, value))
                {
                    LoadPreview();
                }
            }
        }

        private string? _warningMessage;
        public string? WarningMessage
        {
            get => _warningMessage;
            set => SetProperty(ref _warningMessage, value);
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

        public ObservableCollection<string> GeneratedFiles { get; } = new();

        public ICommand SelectFileCommand { get; }
        public ICommand SplitCommand { get; }
        public ICommand NextPreviewCommand { get; }
        public ICommand PrevPreviewCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }
        public ICommand CloseFileCommand { get; }

        public SplitViewModel()
        {
            SelectFileCommand = new RelayCommand(SelectFile);
            SplitCommand = new RelayCommand(async () => await ExecuteSplitAsync(), () => !string.IsNullOrEmpty(SelectedFilePath) && !IsBusy);
            NextPreviewCommand = new RelayCommand(() => PreviewPageIndex++, () => PreviewPageIndex < PageCount - 1);
            PrevPreviewCommand = new RelayCommand(() => PreviewPageIndex--, () => PreviewPageIndex > 0);
            OpenOutputFolderCommand = new RelayCommand(() =>
            {
                var folder = StorageManager.Instance.OutputsDirectory;
                if (Directory.Exists(folder))
                {
                    Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
                }
            });
            CloseFileCommand = new RelayCommand(CloseFile, () => !string.IsNullOrEmpty(SelectedFilePath) && !IsBusy);
        }

        private void CloseFile()
        {
            SelectedFilePath = "";
            PageCount = 0;
            PreviewImage = null;
            GeneratedFiles.Clear();
            StatusMessage = "Dosya kapatıldı.";
            
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
                Title = "Ayrılacak PDF Dosyasını Seçin",
                Filter = "PDF Belgeleri (*.pdf)|*.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                SelectedFilePath = dlg.FileName;
            }
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private bool _isIndeterminate = true;
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetProperty(ref _isIndeterminate, value);
        }

        private async void OnSelectedFileChanged()
        {
            OnPropertyChanged(nameof(SelectedFileName));
            GeneratedFiles.Clear();
            StatusMessage = "";
            WarningMessage = null;

            if (string.IsNullOrEmpty(SelectedFilePath) || !File.Exists(SelectedFilePath))
            {
                PageCount = 0;
                PreviewImage = null;
                return;
            }

            IsBusy = true;
            IsIndeterminate = true;
            StatusMessage = "Dosya analiz ediliyor (büyük dosyalar için zaman alabilir)...";

            try
            {
                await Task.Run(() =>
                {
                    var pCount = PdfRendererService.GetPageCount(SelectedFilePath);
                    System.Windows.Application.Current.Dispatcher.Invoke(() => PageCount = pCount);

                    var warningInfo = PdfInspectionService.InspectPdf(SelectedFilePath);
                    if (warningInfo.HasAnyWarning)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => WarningMessage = string.Join("\n", warningInfo.WarningMessages));
                    }
                });

                _previewPageIndex = 0;
                OnPropertyChanged(nameof(PreviewPageIndex));
                LoadPreview();
                StatusMessage = "Dosya yüklendi ve işleme hazır.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void LoadPreview()
        {
            if (string.IsNullOrEmpty(SelectedFilePath) || PageCount == 0)
            {
                PreviewImage = null;
                return;
            }

            PreviewImage = PdfRendererService.RenderPageToBitmap(SelectedFilePath, _previewPageIndex, 500);
        }

        private async Task ExecuteSplitAsync()
        {
            if (string.IsNullOrEmpty(SelectedFilePath)) return;

            IsBusy = true;
            IsIndeterminate = false;
            ProgressValue = 0;
            StatusMessage = "PDF sayfaları ayrıştırılıyor...";
            GeneratedFiles.Clear();

            try
            {
                var progress = new Progress<double>(percent =>
                {
                    ProgressValue = percent;
                    StatusMessage = $"PDF sayfaları ayrıştırılıyor... %{percent:F1}";
                });

                var result = await Task.Run(() => PdfSplitService.SplitPdf(
                    SelectedFilePath,
                    SelectedMode,
                    RangeText,
                    EveryNPages,
                    progress));

                if (result.IsSuccess)
                {
                    StatusMessage = result.Message;
                    foreach (var f in result.OutputFiles)
                    {
                        GeneratedFiles.Add(f);
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
    }
}
