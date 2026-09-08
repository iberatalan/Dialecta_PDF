using System;
using System.IO;
using System.Windows.Input;
using PdfMaster.Services;

namespace PdfMaster.ViewModels
{
    public enum NavigationTab
    {
        Merge,
        Split,
        Organize,
        Compress,
        Watermark,
        Redact,
        Ocr,
        Sign,
        ImageToPdf,
        Audit
    }

    public class MainViewModel : ViewModelBase
    {
        private NavigationTab _currentTab = NavigationTab.Merge;
        public NavigationTab CurrentTab
        {
            get => _currentTab;
            set
            {
                if (SetProperty(ref _currentTab, value))
                {
                    OnPropertyChanged(nameof(IsMergeSelected));
                    OnPropertyChanged(nameof(IsSplitSelected));
                    OnPropertyChanged(nameof(IsOrganizeSelected));
                    OnPropertyChanged(nameof(IsCompressSelected));
                    OnPropertyChanged(nameof(IsWatermarkSelected));
                    OnPropertyChanged(nameof(IsRedactSelected));
                    OnPropertyChanged(nameof(IsOcrSelected));
                    OnPropertyChanged(nameof(IsSignSelected));
                    OnPropertyChanged(nameof(IsImageToPdfSelected));
                    OnPropertyChanged(nameof(IsAuditSelected));
                }
            }
        }

        public bool IsMergeSelected
        {
            get => CurrentTab == NavigationTab.Merge;
            set { if (value) CurrentTab = NavigationTab.Merge; }
        }

        public bool IsSplitSelected
        {
            get => CurrentTab == NavigationTab.Split;
            set { if (value) CurrentTab = NavigationTab.Split; }
        }

        public bool IsOrganizeSelected
        {
            get => CurrentTab == NavigationTab.Organize;
            set { if (value) CurrentTab = NavigationTab.Organize; }
        }

        public bool IsCompressSelected
        {
            get => CurrentTab == NavigationTab.Compress;
            set { if (value) CurrentTab = NavigationTab.Compress; }
        }

        public bool IsWatermarkSelected
        {
            get => CurrentTab == NavigationTab.Watermark;
            set { if (value) CurrentTab = NavigationTab.Watermark; }
        }

        public bool IsRedactSelected
        {
            get => CurrentTab == NavigationTab.Redact;
            set { if (value) CurrentTab = NavigationTab.Redact; }
        }

        public bool IsOcrSelected
        {
            get => CurrentTab == NavigationTab.Ocr;
            set { if (value) CurrentTab = NavigationTab.Ocr; }
        }

        public bool IsSignSelected
        {
            get => CurrentTab == NavigationTab.Sign;
            set { if (value) CurrentTab = NavigationTab.Sign; }
        }

        public bool IsImageToPdfSelected
        {
            get => CurrentTab == NavigationTab.ImageToPdf;
            set { if (value) CurrentTab = NavigationTab.ImageToPdf; }
        }

        public bool IsAuditSelected
        {
            get => CurrentTab == NavigationTab.Audit;
            set
            {
                if (value)
                {
                    CurrentTab = NavigationTab.Audit;
                    AuditVM.LoadEvents();
                }
            }
        }

        public MergeViewModel MergeVM { get; } = new();
        public SplitViewModel SplitVM { get; } = new();
        public OrganizeViewModel OrganizeVM { get; } = new();
        public CompressViewModel CompressVM { get; } = new();
        public WatermarkViewModel WatermarkVM { get; } = new();
        public RedactViewModel RedactVM { get; } = new();
        public OcrViewModel OcrVM { get; } = new();
        public SignViewModel SignVM { get; } = new();
        public ImageToPdfViewModel ImageToPdfVM { get; } = new();
        public AuditViewModel AuditVM { get; } = new();

        public string StorageLocation => StorageManager.Instance.BaseDirectory;

        public ICommand NavigateCommand { get; }

        public MainViewModel()
        {
            NavigateCommand = new RelayCommand(param =>
            {
                if (param is NavigationTab tab)
                {
                    CurrentTab = tab;
                    if (tab == NavigationTab.Audit)
                    {
                        AuditVM.LoadEvents();
                    }
                }
                else if (param is string strTab && Enum.TryParse<NavigationTab>(strTab, out var parsed))
                {
                    CurrentTab = parsed;
                    if (parsed == NavigationTab.Audit)
                    {
                        AuditVM.LoadEvents();
                    }
                }
            });
        }
    }
}
