using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Forms; // Cursor, VirtualScreen
using System.Windows.Media;

namespace UltAssist.Vision
{
    public partial class RoiSelectorWindow : Window
    {
        private System.Drawing.Point startPx;
        private bool dragging;
        private double dpiX = 1.0, dpiY = 1.0;
        private System.Drawing.Rectangle virtualScreenPx;
        public System.Drawing.Rectangle SelectedRectPixels { get; private set; }

        public RoiSelectorWindow()
        {
            InitializeComponent();
            var dpi = VisualTreeHelper.GetDpi(this);
            dpiX = dpi.DpiScaleX;
            dpiY = dpi.DpiScaleY;
            virtualScreenPx = SystemInformation.VirtualScreen;

            Left = virtualScreenPx.Left / dpiX;
            Top = virtualScreenPx.Top / dpiY;
            Width = virtualScreenPx.Width / dpiX;
            Height = virtualScreenPx.Height / dpiY;
            WindowState = WindowState.Normal;

            RootCanvas.MouseLeftButtonDown += OnDown;
            RootCanvas.MouseMove += OnMove;
            RootCanvas.MouseLeftButtonUp += OnUp;
            Cursor = System.Windows.Input.Cursors.Cross;
        }

        private void OnDown(object sender, MouseButtonEventArgs e)
        {
            dragging = true;
            startPx = System.Windows.Forms.Cursor.Position;
            SelectionRect.Visibility = Visibility.Visible;
            var dip = ScreenToWindowDip(startPx);
            Canvas.SetLeft(SelectionRect, dip.X);
            Canvas.SetTop(SelectionRect, dip.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
        }

        private void OnMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!dragging) return;
            var curPx = System.Windows.Forms.Cursor.Position;
            var a = ScreenToWindowDip(startPx);
            var b = ScreenToWindowDip(curPx);
            double x = Math.Min(a.X, b.X);
            double y = Math.Min(a.Y, b.Y);
            double w = Math.Abs(b.X - a.X);
            double h = Math.Abs(b.Y - a.Y);
            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = w;
            SelectionRect.Height = h;
        }

        private void OnUp(object sender, MouseButtonEventArgs e)
        {
            if (!dragging) return;
            dragging = false;
            var endPx = System.Windows.Forms.Cursor.Position;
            int x = Math.Min(startPx.X, endPx.X);
            int y = Math.Min(startPx.Y, endPx.Y);
            int w = Math.Abs(endPx.X - startPx.X);
            int h = Math.Abs(endPx.Y - startPx.Y);
            SelectedRectPixels = new System.Drawing.Rectangle(x, y, w, h);
            DialogResult = true;
            Close();
        }

        private System.Windows.Point ScreenToWindowDip(System.Drawing.Point ptPx)
        {
            double xDip = (ptPx.X - virtualScreenPx.Left) / dpiX;
            double yDip = (ptPx.Y - virtualScreenPx.Top) / dpiY;
            return new System.Windows.Point(xDip, yDip);
        }
    }
}


