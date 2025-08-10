using System.Collections.Generic;

namespace UltAssist.Config
{
    public class RoiNormalized
    {
        public float X { get; set; }   // 0..1
        public float Y { get; set; }   // 0..1
        public float W { get; set; }   // 0..1
        public float H { get; set; }   // 0..1
    }

    public class VisionConfig
    {
        public bool Enabled { get; set; } = false;
        public RoiNormalized? Roi { get; set; }
        public List<string> OpenTemplates { get; set; } = new();
        public List<string> CloseTemplates { get; set; } = new();
        public double OpenThreshold { get; set; } = 0.85;
        public double CloseThreshold { get; set; } = 0.85;
        public int RequiredConsecutiveFrames { get; set; } = 3;
        public int CaptureFps { get; set; } = 8;
        public string DefaultTemplatesRoot { get; set; } = string.Empty; // 可选：默认模板根目录
    }

    public class HeroConfig
    {
        public string Hero { get; set; } = "Default";
        public string AudioPath { get; set; } = string.Empty;
        public float Volume { get; set; } = 0.85f;
        public int MaxDurationMs { get; set; } = 12000;
        public bool Loop { get; set; } = false;
        public string TemplatesDir { get; set; } = string.Empty; // 可选：此英雄模板目录
        public List<string> OpenTemplates { get; set; } = new(); // 此英雄专用开大模板
        public List<string> CloseTemplates { get; set; } = new(); // 此英雄专用收大模板
    }

    public class AppConfig
    {
        public string HeadphoneDeviceId { get; set; } = string.Empty;
        public string VirtualMicDeviceId { get; set; } = string.Empty;
        public string Hotkey { get; set; } = "V";
        public float FadeInMs { get; set; } = 120f;
        public float FadeOutMs { get; set; } = 150f;
        public List<HeroConfig> Heroes { get; set; } = new();
        public string CurrentHero { get; set; } = "Default";
        public VisionConfig Vision { get; set; } = new();
    }
}

