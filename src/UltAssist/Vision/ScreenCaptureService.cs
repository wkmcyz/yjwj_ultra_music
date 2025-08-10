using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace UltAssist.Vision
{
    public static class ScreenCaptureService
    {
        public static Bitmap CaptureRoi(Rectangle roi)
        {
            if (roi.Width <= 0 || roi.Height <= 0)
                throw new ArgumentException("ROI size must be positive.");

            var bmp = new Bitmap(roi.Width, roi.Height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(roi.X, roi.Y, 0, 0, new Size(roi.Width, roi.Height));
            return bmp;
        }

        public static void SaveBitmap(Bitmap bmp, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            bmp.Save(path, ImageFormat.Png);
        }
    }
}


