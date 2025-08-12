using System;
using System.Collections.Generic;
using System.Linq;

namespace UltAssist.Config
{
    // V2 配置模型：从大招识别改为按键映射系统
    public class AppConfigV2
    {
        public GlobalSettings Global { get; set; } = new();
        public Dictionary<string, HeroConfigV2> HeroConfigs { get; set; } = new();
        public string CurrentHero { get; set; } = string.Empty;
    }

    public class GlobalSettings
    {
        // 音频设备配置
        public string HeadphoneDeviceId { get; set; } = string.Empty;
        public string VirtualMicDeviceId { get; set; } = string.Empty;
        public bool TemporarilySetDefaultMic { get; set; } = false;

        // 监听配置
        public ListeningMode ListeningMode { get; set; } = ListeningMode.GameWindowOnly;
        public List<string> GameProcessNames { get; set; } = new() { "NarakaBladepoint.exe" };
        public bool GlobalListenerEnabled { get; set; } = true; // Ctrl+1 开关状态

        // 顶部指示栏配置
        public OverlaySettings Overlay { get; set; } = new();

        // 调试设置
        public bool DebugMode { get; set; } = false;
    }

    public enum ListeningMode
    {
        GameWindowOnly, // 仅游戏窗口焦点时监听
        Global          // 全局监听
    }

    public class OverlaySettings
    {
        public OverlayStyle Style { get; set; } = OverlayStyle.None;
        public OverlayPosition Position { get; set; } = OverlayPosition.TopLeft;
        public bool Enabled { get; set; } = false;
    }

    public enum OverlayStyle
    {
        None,           // 不显示
        StatusOnly,     // 仅显示开启/关闭状态
        DebugPanel      // 显示详细调试信息
    }

    public enum OverlayPosition
    {
        TopLeft,
        TopRight,
        TopCenter
    }

    public class HeroConfigV2
    {
        public string Name { get; set; } = string.Empty;
        public List<KeyMapping> KeyMappings { get; set; } = new();
    }

    public class KeyMapping
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public KeyCombination Keys { get; set; } = new();
        public AudioSettings Audio { get; set; } = new();
        public string DisplayName { get; set; } = string.Empty; // 用户自定义别名
    }

    public class KeyCombination
    {
        public List<string> Keys { get; set; } = new(); // 如: ["Ctrl", "C"] 或 ["V"] 或 ["Alt", "LeftMouse"]
        
        public string ToDisplayString()
        {
            if (Keys.Count == 0) return "未设置";
            return string.Join(" + ", Keys);
        }

        public bool IsEmpty => Keys.Count == 0;

        // 判断两个组合键是否相同
        public bool Equals(KeyCombination? other)
        {
            if (other == null) return false;
            if (Keys.Count != other.Keys.Count) return false;
            var sorted1 = Keys.OrderBy(k => k).ToList();
            var sorted2 = other.Keys.OrderBy(k => k).ToList();
            return sorted1.SequenceEqual(sorted2);
        }

        // 判断当前组合键是否包含另一个（用于优先级判断：组合键优先于单键）
        public bool Contains(KeyCombination other)
        {
            return other.Keys.All(k => Keys.Contains(k));
        }
    }

    public class AudioSettings
    {
        public string FilePath { get; set; } = string.Empty;
        public float Volume { get; set; } = 0.7f; // 0.0 - 1.0
        public int FadeInMs { get; set; } = 200;
        public int FadeOutMs { get; set; } = 150;
        public bool Loop { get; set; } = false;
        public bool Interruptible { get; set; } = true; // 是否可被其他按键打断
    }

    // 默认配置生成器
    public static class DefaultsV2
    {
        public static AppConfigV2 CreateDefault()
        {
            var config = new AppConfigV2();
            
            // 添加默认英雄配置
            var defaultHeroes = new[]
            {
                "季沧海", "迦南", "妖刀姬", "武田信忠", "顾清寒", 
                "胡桃", "无尘", "宁红夜", "特木尔", "崔三娘", 
                "天海", "玉玲珑", "席拉", "岳山"
            };

            foreach (var hero in defaultHeroes)
            {
                config.HeroConfigs[hero] = new HeroConfigV2 { Name = hero };
            }

            config.CurrentHero = defaultHeroes[0];
            return config;
        }

        public static KeyMapping CreateDefaultKeyMapping(string keys = "V")
        {
            return new KeyMapping
            {
                Keys = new KeyCombination { Keys = new List<string> { keys } },
                Audio = new AudioSettings
                {
                    Volume = 0.7f,
                    FadeInMs = 200,
                    FadeOutMs = 150,
                    Loop = false,
                    Interruptible = true
                },
                DisplayName = $"默认 {keys} 键"
            };
        }
    }
}
