using System.Windows;
using System.Windows.Controls;
using PdfMaster.ViewModels;

namespace PdfMaster.Views
{
    public partial class SplitView : UserControl
    {
        public SplitView()
        {
            InitializeComponent();
        }

        private void UserControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void UserControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && DataContext is SplitViewModel vm)
                {
                    vm.SetFilePath(files[0]);
                }
            }
        }
    }
}
