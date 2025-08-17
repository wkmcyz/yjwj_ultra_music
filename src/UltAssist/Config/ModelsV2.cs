using System;
using System.Collections.Generic;
using System.Linq;

namespace UltAssist.Config
{
    // V2 配置模型：通用按键映射系统，适用于所有游戏
    public class AppConfigV2
    {
        public GlobalSettings Global { get; set; } = new();
        public List<ConfigProfile> Profiles { get; set; } = new();
        public string CurrentProfile { get; set; } = string.Empty;
    }

    public class GlobalSettings
    {
        // 音频设备配置
        public string HeadphoneDeviceId { get; set; } = string.Empty;
        public string VirtualMicDeviceId { get; set; } = string.Empty;
        public bool TemporarilySetDefaultMic { get; set; } = false;

        // 监听配置
        public ListeningMode ListeningMode { get; set; } = ListeningMode.Global;
        public List<string> GameProcessNames { get; set; } = new();
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

    public class ConfigProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty; // 配置描述，如"永劫无间"、"CSGO"等
        public List<KeyMapping> KeyMappings { get; set; } = new();
    }

    public class KeyMapping
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public KeyCombination Keys { get; set; } = new();
        public AudioSettings Audio { get; set; } = new();
        public string DisplayName { get; set; } = string.Empty; // 用户自定义别名
        public bool ExactMatch { get; set; } = true; // 精准匹配：true=完全一致，false=包含匹配
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

        // 根据匹配模式判断是否匹配
        public bool Matches(KeyCombination other, bool exactMatch)
        {
            if (exactMatch)
            {
                // 精准匹配：完全一致
                return Equals(other);
            }
            else
            {
                // 包含匹配：other包含当前配置的所有按键
                return other.Contains(this);
            }
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
}
