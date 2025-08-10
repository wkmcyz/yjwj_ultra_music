using System.Collections.Generic;

namespace UltAssist.Config
{
    public class HeroConfig
    {
        public string Hero { get; set; } = "Default";
        public string AudioPath { get; set; } = string.Empty;
        public float Volume { get; set; } = 0.85f;
        public int MaxDurationMs { get; set; } = 12000;
        public bool Loop { get; set; } = false;
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
    }
}

