using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PdfMaster.Models;
using PdfMaster.Services;

namespace PdfMaster.ViewModels
{
    public class RedactViewModel : ViewModelBase
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

        public ObservableCollection<string> Keywords { get; } = new() { "TCKN", "12345678901", "Gizli" };

        private string _newKeyword = "";
        public string NewKeyword
        {
            get => _newKeyword;
            set => SetProperty(ref _newKeyword, value);
        }

        private string? _selectedKeyword;
        public string? SelectedKeyword
        {
            get => _selectedKeyword;
            set => SetProperty(ref _selectedKeyword, value);
        }

        private bool _matchWholeWord = false;
        public bool MatchWholeWord
        {
            get => _matchWholeWord;
            set => SetProperty(ref _matchWholeWord, value);
        }

        private bool _caseSensitive = false;
        public bool CaseSensitive
        {
            get => _caseSensitive;
            set => SetProperty(ref _caseSensitive, value);
        }

        private bool _overlayText = true;
        public bool OverlayText
        {
            get => _overlayText;
            set => SetProperty(ref _overlayText, value);
        }

        private string _overlayTextContent = "[GİZLENDİ]";
        public string OverlayTextContent
        {
            get => _overlayTextContent;
            set => SetProperty(ref _overlayTextContent, value);
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

        public ICommand SelectFileCommand { get; }
        public ICommand AddKeywordCommand { get; }
        public ICommand RemoveKeywordCommand { get; }
        public ICommand RedactCommand { get; }
        public ICommand OpenOutputFileCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }
        public ICommand CloseFileCommand { get; }

        public RedactViewModel()
        {
            SelectFileCommand = new RelayCommand(SelectFile);
            AddKeywordCommand = new RelayCommand(AddKeyword, () => !string.IsNullOrWhiteSpace(NewKeyword));
            RemoveKeywordCommand = new RelayCommand(RemoveKeyword, () => !string.IsNullOrEmpty(SelectedKeyword));
            RedactCommand = new RelayCommand(async () => await ExecuteRedactAsync(), () => !string.IsNullOrEmpty(SelectedFilePath) && Keywords.Count > 0 && !IsBusy);
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
                Title = "Karartma Yapılacak PDF Dosyasını Seçin",
                Filter = "PDF Belgeleri (*.pdf)|*.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                SelectedFilePath = dlg.FileName;
            }
        }

        private void AddKeyword()
        {
            if (!string.IsNullOrWhiteSpace(NewKeyword) && !Keywords.Contains(NewKeyword.Trim()))
            {
                Keywords.Add(NewKeyword.Trim());
                NewKeyword = "";
            }
        }

        private void RemoveKeyword()
        {
            if (!string.IsNullOrEmpty(SelectedKeyword))
            {
                Keywords.Remove(SelectedKeyword);
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

            PreviewImage = PdfRendererService.RenderPageToBitmap(SelectedFilePath, 0, 500);
        }

        private async Task ExecuteRedactAsync()
        {
            if (string.IsNullOrEmpty(SelectedFilePath) || Keywords.Count == 0) return;

            IsBusy = true;
            StatusMessage = "Hassas kelimeler aranıyor ve kalıcı olarak karartılıyor...";
            LastOutputFile = null;

            try
            {
                var options = new RedactionOptions
                {
                    KeywordsToRedact = Keywords.ToList(),
                    MatchWholeWord = MatchWholeWord,
                    CaseSensitive = CaseSensitive,
                    FillColorHex = "#000000",
                    OverlayText = OverlayText,
                    OverlayTextContent = OverlayTextContent
                };

                var result = await Task.Run(() => PdfRedactionService.RedactPdf(SelectedFilePath, options));

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
