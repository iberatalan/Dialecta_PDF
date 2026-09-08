using System.Windows.Media.Imaging;

namespace PdfMaster.Models
{
    public class PdfPageItem : System.ComponentModel.INotifyPropertyChanged
    {
        public int PageIndex { get; set; } // 0-based
        public int PageNumber => PageIndex + 1; // 1-based display
        public int OriginalPageIndex { get; set; }
        
        private int _rotationAngle = 0;
        public int RotationAngle
        {
            get => _rotationAngle;
            set
            {
                if (_rotationAngle != value)
                {
                    _rotationAngle = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(RotationAngle)));
                }
            }
        } // 0, 90, 180, 270

        public BitmapSource? Thumbnail { get; set; }
        public bool IsSelected { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        private bool _isDeleted;
        public bool IsDeleted
        {
            get => _isDeleted;
            set
            {
                if (_isDeleted != value)
                {
                    _isDeleted = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsDeleted)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public void RotateClockwise()
        {
            RotationAngle = (RotationAngle + 90) % 360;
        }

        public void RotateCounterClockwise()
        {
            RotationAngle = (RotationAngle + 270) % 360;
        }
    }
}
