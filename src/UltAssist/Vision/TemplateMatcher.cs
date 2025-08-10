using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace UltAssist.Vision
{
    public static class TemplateMatcher
    {
        public static double MatchAny(Bitmap source, IEnumerable<string> templates, out string? hit)
        {
            hit = null;
            if (templates == null) return 0;
            using var src = BitmapConverter.ToMat(source);
            using var srcGray = ToGray(src);

            double best = 0;
            string? bestName = null;
            foreach (var path in templates.Where(System.IO.File.Exists))
            {
                using var temp = Cv2.ImRead(path, ImreadModes.Grayscale);
                if (temp.Empty()) continue;
                using var result = new Mat();
                Cv2.MatchTemplate(srcGray, temp, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var max, out _, out _);
                if (max > best)
                {
                    best = max;
                    bestName = path;
                }
            }

            hit = bestName;
            return best;
        }

        private static Mat ToGray(Mat src)
        {
            if (src.Channels() == 1) return src.Clone();
            var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            return gray;
        }
    }
}


