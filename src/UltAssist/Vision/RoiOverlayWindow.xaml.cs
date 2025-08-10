using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace UltAssist.Vision
{
    public partial class RoiOverlayWindow : Window
    {
        private double dpiX = 1.0, dpiY = 1.0;
        public RoiOverlayWindow()
        {
            InitializeComponent();
            var dpi = VisualTreeHelper.GetDpi(this);
            dpiX = dpi.DpiScaleX;
            dpiY = dpi.DpiScaleY;
            IsHitTestVisible = false;
        }

        public void ShowAt(System.Drawing.Rectangle roiPx)
        {
            var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
            Left = vs.Left / dpiX;
            Top = vs.Top / dpiY;
            Width = vs.Width / dpiX;
            Height = vs.Height / dpiY;
            WindowState = WindowState.Normal;
            Topmost = true;
            Show();

            double x = (roiPx.X - vs.Left) / dpiX;
            double y = (roiPx.Y - vs.Top) / dpiY;
            double w = roiPx.Width / dpiX;
            double h = roiPx.Height / dpiY;
            Canvas.SetLeft(RoiRect, x);
            Canvas.SetTop(RoiRect, y);
            RoiRect.Width = w;
            RoiRect.Height = h;
        }
    }
}


