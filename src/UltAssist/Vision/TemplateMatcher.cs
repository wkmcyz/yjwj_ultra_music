using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace UltAssist.Vision
{
    public static class TemplateMatcher
    {
        // 多尺度 + 边缘化 + 可选掩码 的匹配。返回最佳分数与命中模板路径
        public static double MatchAny(Bitmap source, IEnumerable<string> templates, out string? hit)
        {
            hit = null;
            if (templates == null) return 0;

            using var srcMat = BitmapConverter.ToMat(source);
            using var srcEdges = PreprocessEdges(srcMat);

            double bestScore = 0.0;
            string? bestPath = null;

            // 多尺度范围（针对模板）
            double[] scales = Enumerable.Range(0, 9).Select(i => 0.85 + i * 0.05).ToArray(); // 0.85~1.25

            foreach (var path in templates.Where(File.Exists))
            {
                using var tplColor = Cv2.ImRead(path, ImreadModes.Color);
                if (tplColor.Empty()) continue;
                using var tplEdgesFull = PreprocessEdges(tplColor);

                // 可选掩码：同名 .mask.png 或 _mask.png
                var maskPath = DeriveMaskPath(path);
                using var tplMaskFull = File.Exists(maskPath) ? Cv2.ImRead(maskPath, ImreadModes.Grayscale) : new Mat();
                if (!tplMaskFull.Empty()) Cv2.Threshold(tplMaskFull, tplMaskFull, 128, 255, ThresholdTypes.Binary);

                foreach (var s in scales)
                {
                    // resize 模板（与掩码保持一致）
                    OpenCvSharp.Size newSize = new OpenCvSharp.Size((int)Math.Max(1, tplEdgesFull.Cols * s), (int)Math.Max(1, tplEdgesFull.Rows * s));
                    if (newSize.Width > srcEdges.Cols || newSize.Height > srcEdges.Rows) continue;

                    using var tplEdges = new Mat();
                    Cv2.Resize(tplEdgesFull, tplEdges, newSize, 0, 0, InterpolationFlags.Area);

                    Mat? tplMask = null;
                    if (!tplMaskFull.Empty())
                    {
                        tplMask = new Mat();
                        Cv2.Resize(tplMaskFull, tplMask, newSize, 0, 0, InterpolationFlags.Nearest);
                    }

                    using var result = new Mat();
                    try
                    {
                        if (tplMask != null && !tplMask.Empty())
                        {
                            Cv2.MatchTemplate(srcEdges, tplEdges, result, TemplateMatchModes.CCorrNormed, tplMask);
                        }
                        else
                        {
                            Cv2.MatchTemplate(srcEdges, tplEdges, result, TemplateMatchModes.CCorrNormed);
                        }
                        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);
                        if (maxVal > bestScore)
                        {
                            bestScore = maxVal;
                            bestPath = path;
                        }
                    }
                    finally
                    {
                        tplMask?.Dispose();
                    }
                }
            }

            hit = bestPath;
            return bestScore;
        }

        private static Mat PreprocessEdges(Mat srcBgr)
        {
            using var gray = new Mat();
            if (srcBgr.Channels() == 1)
                srcBgr.CopyTo(gray);
            else
                Cv2.CvtColor(srcBgr, gray, ColorConversionCodes.BGR2GRAY);

            using var blur = new Mat();
            Cv2.GaussianBlur(gray, blur, new OpenCvSharp.Size(3, 3), 0);

            var edges = new Mat();
            Cv2.Canny(blur, edges, 60, 120);

            // 轻度膨胀让细线更连贯
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Cv2.Dilate(edges, edges, kernel);
            return edges;
        }

        private static string DeriveMaskPath(string templatePath)
        {
            var dir = Path.GetDirectoryName(templatePath) ?? string.Empty;
            var nameNoExt = Path.GetFileNameWithoutExtension(templatePath);
            var p1 = Path.Combine(dir, nameNoExt + ".mask.png");
            if (File.Exists(p1)) return p1;
            var p2 = Path.Combine(dir, nameNoExt + "_mask.png");
            return p2;
        }
    }
}


