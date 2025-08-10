using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace UltAssist.Vision
{
    public sealed class DatasetCaptureService : IDisposable
    {
        private CancellationTokenSource? cts;

        public bool IsRunning => cts != null && !cts.IsCancellationRequested;

        public void Start(Rectangle roi, string datasetRoot, string hero, int intervalMs = 1000)
        {
            Stop();
            cts = new CancellationTokenSource();
            var baseDir = Path.Combine(datasetRoot, "raw", hero);
            Directory.CreateDirectory(baseDir);
            // Ensure three state folders for convenience
            Directory.CreateDirectory(Path.Combine(baseDir, "charging"));
            Directory.CreateDirectory(Path.Combine(baseDir, "ready"));
            Directory.CreateDirectory(Path.Combine(baseDir, "release"));
            _ = LoopCaptureAsync(roi, datasetRoot, hero, intervalMs, cts.Token);
        }

        public void Stop()
        {
            try { cts?.Cancel(); } catch { }
            cts = null;
        }

        private static async Task LoopCaptureAsync(Rectangle roi, string root, string hero, int intervalMs, CancellationToken token)
        {
            var targetDir = Path.Combine(root, "raw", hero);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var bmp = ScreenCaptureService.CaptureRoi(roi);
                    var name = $"roi_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                    var path = Path.Combine(targetDir, name);
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                catch { }
                try { await Task.Delay(intervalMs, token); } catch { }
            }
        }

        public static void AutoLabelUsingRoiBox(string datasetRoot, string hero, Rectangle roiBoxOnImage,
            string chargingDir, string readyDir, string releaseDir,
            string pythonRoot)
        {
            // Ensure output dirs
            var imgTrain = Path.Combine(pythonRoot, "datasets", "images", "train");
            var lblTrain = Path.Combine(pythonRoot, "datasets", "labels", "train");
            Directory.CreateDirectory(imgTrain);
            Directory.CreateDirectory(lblTrain);

            // Prepare class name mapping file (python/datasets/ult.names)
            var namesFile = Path.Combine(pythonRoot, "datasets", "ult.names");
            var names = new System.Collections.Generic.List<string>();
            if (File.Exists(namesFile))
            {
                names.AddRange(File.ReadAllLines(namesFile));
            }
            int idCharging = EnsureClass(names, hero + "_charging");
            int idReady = EnsureClass(names, hero + "_ready");
            int idRelease = EnsureClass(names, hero + "_release");
            Directory.CreateDirectory(Path.GetDirectoryName(namesFile)!);
            File.WriteAllLines(namesFile, names);

            // Label three folders
            CopyAndLabelFolder(chargingDir, idCharging, imgTrain, lblTrain, roiBoxOnImage);
            CopyAndLabelFolder(readyDir, idReady, imgTrain, lblTrain, roiBoxOnImage);
            CopyAndLabelFolder(releaseDir, idRelease, imgTrain, lblTrain, roiBoxOnImage);
        }

        public static void AutoLabelUsingRoiBoxes(string datasetRoot, string hero,
            Rectangle roiCharging, Rectangle roiReady, Rectangle roiRelease,
            string chargingDir, string readyDir, string releaseDir,
            string pythonRoot)
        {
            var imgTrain = Path.Combine(pythonRoot, "datasets", "images", "train");
            var lblTrain = Path.Combine(pythonRoot, "datasets", "labels", "train");
            var imgVal = Path.Combine(pythonRoot, "datasets", "images", "val");
            var lblVal = Path.Combine(pythonRoot, "datasets", "labels", "val");
            Directory.CreateDirectory(imgTrain);
            Directory.CreateDirectory(lblTrain);
            Directory.CreateDirectory(imgVal);
            Directory.CreateDirectory(lblVal);

            var namesFile = Path.Combine(pythonRoot, "datasets", "ult.names");
            var names = new System.Collections.Generic.List<string>();
            if (File.Exists(namesFile)) names.AddRange(File.ReadAllLines(namesFile));
            int idCharging = EnsureClass(names, hero + "_charging");
            int idReady = EnsureClass(names, hero + "_ready");
            int idRelease = EnsureClass(names, hero + "_release");
            Directory.CreateDirectory(Path.GetDirectoryName(namesFile)!);
            File.WriteAllLines(namesFile, names);

            CopySplitAndLabelFolder(chargingDir, idCharging, imgTrain, lblTrain, imgVal, lblVal, roiCharging);
            CopySplitAndLabelFolder(readyDir, idReady, imgTrain, lblTrain, imgVal, lblVal, roiReady);
            CopySplitAndLabelFolder(releaseDir, idRelease, imgTrain, lblTrain, imgVal, lblVal, roiRelease);

            // Generate datasets/ult.yaml from names
            var yaml = "path: ./datasets\ntrain: images/train\nval: images/val\n\nnames:\n";
            for (int i = 0; i < names.Count; i++) yaml += $"  {i}: {names[i]}\n";
            File.WriteAllText(Path.Combine(pythonRoot, "datasets", "ult.yaml"), yaml);
        }

        private static int EnsureClass(System.Collections.Generic.List<string> names, string cls)
        {
            int idx = names.FindIndex(s => string.Equals(s?.Trim(), cls, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) return idx;
            names.Add(cls);
            return names.Count - 1;
        }

        private static void CopyAndLabelFolder(string? srcDir, int clsId, string imgTrain, string lblTrain, Rectangle roiBox)
        {
            if (string.IsNullOrWhiteSpace(srcDir) || !Directory.Exists(srcDir)) return;
            foreach (var file in Directory.EnumerateFiles(srcDir))
            {
                try
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;
                    using var bmp = new Bitmap(file);
                    // normalize bbox relative to image size
                    float imgW = bmp.Width;
                    float imgH = bmp.Height;
                    float x = roiBox.X + roiBox.Width / 2f;
                    float y = roiBox.Y + roiBox.Height / 2f;
                    float w = roiBox.Width;
                    float h = roiBox.Height;
                    // clamp
                    if (w <= 0 || h <= 0) { x = imgW / 2f; y = imgH / 2f; w = imgW; h = imgH; }
                    float cx = x / imgW;
                    float cy = y / imgH;
                    float nw = w / imgW;
                    float nh = h / imgH;

                    var baseName = Path.GetFileNameWithoutExtension(file);
                    var targetImg = Path.Combine(imgTrain, baseName + ext);
                    var targetLbl = Path.Combine(lblTrain, baseName + ".txt");
                    File.Copy(file, targetImg, overwrite: true);
                    File.WriteAllText(targetLbl, $"{clsId} {cx:F6} {cy:F6} {nw:F6} {nh:F6}\n");
                }
                catch { }
            }
        }

        private static void CopySplitAndLabelFolder(string? srcDir, int clsId,
            string imgTrain, string lblTrain, string imgVal, string lblVal, Rectangle roiBox,
            double valRatio = 0.2)
        {
            if (string.IsNullOrWhiteSpace(srcDir) || !Directory.Exists(srcDir)) return;
            var rng = new Random(12345);
            foreach (var file in Directory.EnumerateFiles(srcDir))
            {
                try
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;
                    using var bmp = new Bitmap(file);
                    float imgW = bmp.Width;
                    float imgH = bmp.Height;
                    float x = roiBox.X + roiBox.Width / 2f;
                    float y = roiBox.Y + roiBox.Height / 2f;
                    float w = roiBox.Width;
                    float h = roiBox.Height;
                    if (w <= 0 || h <= 0) { x = imgW / 2f; y = imgH / 2f; w = imgW; h = imgH; }
                    float cx = x / imgW;
                    float cy = y / imgH;
                    float nw = w / imgW;
                    float nh = h / imgH;

                    var baseName = Path.GetFileNameWithoutExtension(file) + "_" + clsId.ToString();
                    bool toVal = rng.NextDouble() < valRatio;
                    var targetImg = Path.Combine(toVal ? imgVal : imgTrain, baseName + ext);
                    var targetLbl = Path.Combine(toVal ? lblVal : lblTrain, baseName + ".txt");
                    File.Copy(file, targetImg, overwrite: true);
                    File.WriteAllText(targetLbl, $"{clsId} {cx:F6} {cy:F6} {nw:F6} {nh:F6}\n");
                }
                catch { }
            }
        }

        public void Dispose() => Stop();
    }
}


