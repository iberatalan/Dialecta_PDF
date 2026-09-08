using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace PdfMaster
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogAndShowCrash("Kritik Hata (AppDomain)", ex);
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                LogAndShowCrash("Arayüz Hatası (Dispatcher)", args.Exception);
                args.Handled = true; // Prevent app crash
            };
        }

        private static void LogAndShowCrash(string title, Exception? ex)
        {
            var msg = ex?.ToString() ?? "Bilinmeyen hata";
            try
            {
                var crashFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hata_kaydi.log");
                File.AppendAllText(crashFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}:\n{msg}\n\n");
            }
            catch { }

            MessageBox.Show($"Uygulama çalışırken bir hata oluştu:\n\n{ex?.Message}\n\nDetaylar hata_kaydi.log dosyasına kaydedildi.", 
                title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
