using System;
using System.Drawing;
using System.IO;

namespace UltAssist.Vision
{
    public static class TemplateStore
    {
        private static string BaseDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");

        public static string Save(Bitmap bmp, string name)
        {
            if (!Directory.Exists(BaseDir)) Directory.CreateDirectory(BaseDir);
            string file = Path.Combine(BaseDir, name + ".png");
            bmp.Save(file);
            return file;
        }
    }
}


