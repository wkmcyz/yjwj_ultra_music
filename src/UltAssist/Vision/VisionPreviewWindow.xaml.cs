using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace UltAssist.Vision
{
    public partial class VisionPreviewWindow : Window
    {
        private CancellationTokenSource? cts;
        private readonly Func<Bitmap?> captureRoi;
        private readonly Func<(double open, double close)> getScores;
        private readonly Func<(Bitmap? open, Bitmap? close)> getTemplates;

        public VisionPreviewWindow(Func<Bitmap?> captureRoi, Func<(double open, double close)> getScores, Func<(Bitmap? open, Bitmap? close)> getTemplates)
        {
            InitializeComponent();
            this.captureRoi = captureRoi;
            this.getScores = getScores;
            this.getTemplates = getTemplates;
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            _ = RunAsync(cts.Token);
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            try { cts?.Cancel(); } catch { }
        }

        private async Task RunAsync(CancellationToken token)
        {
            var sw = new Stopwatch();
            while (!token.IsCancellationRequested)
            {
                sw.Restart();
                try
                {
                    using var bmp = captureRoi();
                    if (bmp != null)
                    {
                        Dispatcher.Invoke(() => PreviewImage.Source = ToBitmapSource(bmp));
                    }
                    // load template previews
                    var (tOpen, tClose) = getTemplates();
                    if (tOpen != null)
                        Dispatcher.Invoke(() => OpenTplImage.Source = ToBitmapSource(tOpen));
                    if (tClose != null)
                        Dispatcher.Invoke(() => CloseTplImage.Source = ToBitmapSource(tClose));
                    var (o, c) = getScores();
                    Dispatcher.Invoke(() =>
                    {
                        OpenScoreText.Text = o.ToString("F2");
                        CloseScoreText.Text = c.ToString("F2");
                    });
                }
                catch { }
                sw.Stop();
                double fps = 1000.0 / Math.Max(1.0, sw.ElapsedMilliseconds);
                Dispatcher.Invoke(() => FpsText.Text = fps.ToString("F1"));
                try { await Task.Delay(100, token); } catch { }
            }
        }

        private static BitmapSource ToBitmapSource(Bitmap bmp)
        {
            using var mem = new MemoryStream();
            bmp.Save(mem, System.Drawing.Imaging.ImageFormat.Png);
            mem.Position = 0;
            var bs = new BitmapImage();
            bs.BeginInit();
            bs.CacheOption = BitmapCacheOption.OnLoad;
            bs.StreamSource = mem;
            bs.EndInit();
            bs.Freeze();
            return bs;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    }
}


