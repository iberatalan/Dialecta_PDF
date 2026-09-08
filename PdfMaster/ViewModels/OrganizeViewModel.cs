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
    public class OrganizeViewModel : ViewModelBase
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

        public ObservableCollection<PdfPageItem> Pages { get; } = new();

        private PdfPageItem? _selectedPage;
        public PdfPageItem? SelectedPage
        {
            get => _selectedPage;
            set => SetProperty(ref _selectedPage, value);
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

        private string? _lastOutputFile;
        public string? LastOutputFile
        {
            get => _lastOutputFile;
            set => SetProperty(ref _lastOutputFile, value);
        }

        public ICommand SelectFileCommand { get; }
        public ICommand RotateLeftCommand { get; }
        public ICommand RotateRightCommand { get; }
        public ICommand RotateAllCommand { get; }
        public ICommand MovePageLeftCommand { get; }
        public ICommand MovePageRightCommand { get; }
        public ICommand ToggleDeletePageCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand OpenOutputFileCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }

        public ICommand CloseFileCommand { get; }

        public OrganizeViewModel()
        {
            SelectFileCommand = new RelayCommand(SelectFile);
            CloseFileCommand = new RelayCommand(CloseFile, () => !string.IsNullOrEmpty(SelectedFilePath));
            RotateLeftCommand = new RelayCommand(RotateLeft, () => SelectedPage != null);
            RotateRightCommand = new RelayCommand(RotateRight, () => SelectedPage != null);
            RotateAllCommand = new RelayCommand(RotateAll, () => Pages.Count > 0);
            MovePageLeftCommand = new RelayCommand(MovePageLeft, () => SelectedPage != null && Pages.IndexOf(SelectedPage) > 0);
            MovePageRightCommand = new RelayCommand(MovePageRight, () => SelectedPage != null && Pages.IndexOf(SelectedPage) < Pages.Count - 1);
            ToggleDeletePageCommand = new RelayCommand(ToggleDeletePage, () => SelectedPage != null);
            SaveChangesCommand = new RelayCommand(async () => await SaveChangesAsync(), () => Pages.Any(p => !p.IsDeleted) && !IsBusy);
            OpenOutputFileCommand = new RelayCommand(OpenOutputFile, () => !string.IsNullOrEmpty(LastOutputFile) && File.Exists(LastOutputFile));
            OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
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
                Title = "Sayfaları Düzenlenecek PDF Dosyasını Seçin",
                Filter = "PDF Belgeleri (*.pdf)|*.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                SelectedFilePath = dlg.FileName;
            }
        }

        private void CloseFile()
        {
            SelectedFilePath = "";
            Pages.Clear();
            SelectedPage = null;
            StatusMessage = "Dosya kapatıldı.";
            LastOutputFile = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private async void OnSelectedFileChanged()
        {
            OnPropertyChanged(nameof(SelectedFileName));
            Pages.Clear();
            StatusMessage = "";
            LastOutputFile = null;

            if (string.IsNullOrEmpty(SelectedFilePath) || !File.Exists(SelectedFilePath))
                return;

            IsBusy = true;
            IsIndeterminate = false;
            ProgressValue = 0;
            StatusMessage = "Sayfa önizlemeleri oluşturuluyor...";

            try
            {
                var progress = new Progress<double>(percent =>
                {
                    ProgressValue = percent;
                    StatusMessage = $"Sayfa önizlemeleri oluşturuluyor... %{percent:F1}";
                });

                int count = PdfRendererService.GetPageCount(SelectedFilePath);
                var items = await Task.Run(async () =>
                {
                    var list = new ObservableCollection<PdfPageItem>();
                    var thumbnails = PdfRendererService.RenderPagesToBitmaps(SelectedFilePath, 0, count, 150, 0, progress);
                    
                    for (int i = 0; i < count; i++)
                    {
                        list.Add(new PdfPageItem
                        {
                            PageIndex = i,
                            OriginalPageIndex = i,
                            RotationAngle = 0,
                            Thumbnail = thumbnails[i]
                        });

                        if (i % 100 == 0)
                        {
                            await Task.Delay(1);
                        }
                    }
                    return list;
                });

                foreach (var item in items)
                {
                    Pages.Add(item);
                }

                if (Pages.Count > 0)
                {
                    SelectedPage = Pages[0];
                }

                StatusMessage = $"{Pages.Count} sayfa yüklendi. Sayfaları döndürebilir veya sıralayabilirsiniz.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Önizleme hatası: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                IsIndeterminate = true;
                ProgressValue = 0;
            }
        }

        private void RotateLeft()
        {
            if (SelectedPage != null)
            {
                SelectedPage.RotateCounterClockwise();
                OnPropertyChanged(nameof(Pages));
            }
        }

        private void RotateRight()
        {
            if (SelectedPage != null)
            {
                SelectedPage.RotateClockwise();
                OnPropertyChanged(nameof(Pages));
            }
        }

        private void RotateAll()
        {
            foreach (var p in Pages)
            {
                p.RotateClockwise();
            }
            OnPropertyChanged(nameof(Pages));
        }

        private void MovePageLeft()
        {
            if (SelectedPage == null) return;
            int idx = Pages.IndexOf(SelectedPage);
            if (idx > 0)
            {
                Pages.Move(idx, idx - 1);
            }
        }

        private void MovePageRight()
        {
            if (SelectedPage == null) return;
            int idx = Pages.IndexOf(SelectedPage);
            if (idx < Pages.Count - 1)
            {
                Pages.Move(idx, idx + 1);
            }
        }

        private void ToggleDeletePage()
        {
            if (SelectedPage != null)
            {
                SelectedPage.IsDeleted = !SelectedPage.IsDeleted;
                OnPropertyChanged(nameof(Pages));
            }
        }

        private async Task SaveChangesAsync()
        {
            if (string.IsNullOrEmpty(SelectedFilePath)) return;

            IsBusy = true;
            StatusMessage = "Düzenlenmiş PDF kaydediliyor...";
            LastOutputFile = null;

            try
            {
                var pageList = Pages.ToList();
                var result = await Task.Run(() => PdfOrganizeService.OrganizeAndRotate(SelectedFilePath, pageList));

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
                StatusMessage = $"Kaydetme hatası: {ex.Message}";
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
