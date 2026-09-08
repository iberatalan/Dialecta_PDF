using System.Windows;
using System.Windows.Controls;
using PdfMaster.ViewModels;

namespace PdfMaster.Views
{
    public partial class ImageToPdfView : UserControl
    {
        public ImageToPdfView()
        {
            InitializeComponent();
        }

        private void UserControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is ImageToPdfViewModel vm)
                {
                    vm.HandleDroppedFiles(files);
                }
            }
        }

        private void UserControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
    }
}
