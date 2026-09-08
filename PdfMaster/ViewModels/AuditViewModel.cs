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
    public class AuditViewModel : ViewModelBase
    {
        public ObservableCollection<AuditEvent> Events { get; } = new();

        private string _searchQuery = "";
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    FilterEvents();
                }
            }
        }

        private AuditEvent? _selectedEvent;
        public AuditEvent? SelectedEvent
        {
            get => _selectedEvent;
            set => SetProperty(ref _selectedEvent, value);
        }

        // Integrity Verification
        private string _verifyFilePath = "";
        public string VerifyFilePath
        {
            get => _verifyFilePath;
            set
            {
                if (SetProperty(ref _verifyFilePath, value))
                {
                    OnVerifyFilePathChanged();
                }
            }
        }

        private string _verifyResultText = "Doğrulamak için bir PDF dosyası seçin veya sürükleyip bırakın.";
        public string VerifyResultText
        {
            get => _verifyResultText;
            set => SetProperty(ref _verifyResultText, value);
        }

        private string _computedHash = "";
        public string ComputedHash
        {
            get => _computedHash;
            set => SetProperty(ref _computedHash, value);
        }

        private bool? _isHashVerified;
        public bool? IsHashVerified
        {
            get => _isHashVerified;
            set => SetProperty(ref _isHashVerified, value);
        }

        private int _retentionDays = 30;
        public int RetentionDays
        {
            get => _retentionDays;
            set => SetProperty(ref _retentionDays, value);
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand RefreshEventsCommand { get; }
        public ICommand SelectVerifyFileCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand CleanupOldFilesCommand { get; }
        public ICommand OpenOutputsFolderCommand { get; }
        public ICommand OpenStorageFolderCommand { get; }

        public AuditViewModel()
        {
            RefreshEventsCommand = new RelayCommand(LoadEvents);
            SelectVerifyFileCommand = new RelayCommand(SelectVerifyFile);
            CreateBackupCommand = new RelayCommand(async () => await CreateBackupAsync());
            CleanupOldFilesCommand = new RelayCommand(CleanupOldFiles);
            OpenOutputsFolderCommand = new RelayCommand(() =>
            {
                var folder = StorageManager.Instance.OutputsDirectory;
                if (Directory.Exists(folder))
                {
                    Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
                }
            });
            OpenStorageFolderCommand = new RelayCommand(() =>
            {
                var folder = StorageManager.Instance.BaseDirectory;
                if (Directory.Exists(folder))
                {
                    Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
                }
            });

            LoadEvents();
        }

        public void LoadEvents()
        {
            Events.Clear();
            var list = AuditLogger.GetAllEvents();
            foreach (var ev in list)
            {
                Events.Add(ev);
            }
        }

        private void FilterEvents()
        {
            Events.Clear();
            var filtered = AuditLogger.SearchEvents(SearchQuery);
            foreach (var ev in filtered)
            {
                Events.Add(ev);
            }
        }

        public void VerifyFile(string filePath)
        {
            if (File.Exists(filePath) && filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                VerifyFilePath = filePath;
            }
        }

        private void SelectVerifyFile()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Bütünlüğü Doğrulanacak PDF Belgesini Seçin",
                Filter = "PDF Belgeleri (*.pdf)|*.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                VerifyFilePath = dlg.FileName;
            }
        }

        private void OnVerifyFilePathChanged()
        {
            if (string.IsNullOrEmpty(VerifyFilePath) || !File.Exists(VerifyFilePath))
            {
                VerifyResultText = "Doğrulamak için bir PDF dosyası seçin.";
                ComputedHash = "";
                IsHashVerified = null;
                return;
            }

            try
            {
                ComputedHash = SecurityAndHashService.ComputeSha256(VerifyFilePath);
                var (found, matchEvent, isOutput) = AuditLogger.VerifyFileInAuditLog(VerifyFilePath);

                if (found && matchEvent != null)
                {
                    IsHashVerified = true;
                    var typeStr = isOutput ? "Çıktı Belgesi" : "Kaynak/Orijinal Belge";
                    VerifyResultText = $"✅ DOĞRULANDI: Bu belge yerel denetim kütüğünde kayıtlıdır ({typeStr}).\nİşlem: {matchEvent.ActionDescription}\nTarih: {matchEvent.TimestampLocal:dd.MM.yyyy HH:mm:ss}\nSHA-256: {ComputedHash}";
                }
                else
                {
                    IsHashVerified = false;
                    VerifyResultText = $"⚠️ KÜTÜKTE BULUNAMADI: Bu dosyanın SHA-256 özeti bu cihazdaki yerel denetim kütüğünde eşleşmedi. Belge harici bir kaynaktan gelmiş veya değiştirilmiş olabilir.\nHesaplanan SHA-256: {ComputedHash}";
                }
            }
            catch (Exception ex)
            {
                IsHashVerified = false;
                VerifyResultText = $"Doğrulama hatası: {ex.Message}";
            }
        }

        private async Task CreateBackupAsync()
        {
            StatusMessage = "Yedekleme arşivi oluşturuluyor...";
            try
            {
                var zipPath = await Task.Run(() => StorageManager.Instance.CreateBackupZip());
                StatusMessage = $"Yedekleme başarıyla tamamlandı: {Path.GetFileName(zipPath)}";
                Process.Start(new ProcessStartInfo { FileName = Path.GetDirectoryName(zipPath)!, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Yedekleme hatası: {ex.Message}";
            }
        }

        private void CleanupOldFiles()
        {
            try
            {
                int count = StorageManager.Instance.CleanupOldOutputs(RetentionDays);
                StatusMessage = $"{RetentionDays} günden eski toplam {count} adet çıktı dosyası güvenle temizlendi.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Temizleme hatası: {ex.Message}";
            }
        }
    }
}
