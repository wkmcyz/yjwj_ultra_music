using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UltAssist.Config
{
    public static class ConfigServiceV2
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        public static AppConfigV2 Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    var defaultConfig = CreateDefaultConfig();
                    Save(defaultConfig);
                    return defaultConfig;
                }

                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfigV2>(json, JsonOptions);
                
                if (config == null)
                {
                    return CreateDefaultConfig();
                }

                // 配置验证和修复
                ValidateAndRepairConfig(config);
                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置加载失败: {ex.Message}");
                return CreateDefaultConfig();
            }
        }

        public static void Save(AppConfigV2 config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置保存失败: {ex.Message}");
            }
        }

        private static void ValidateAndRepairConfig(AppConfigV2 config)
        {
            // 确保有基本的全局设置
            config.Global ??= new GlobalSettings();
            config.Profiles ??= new();

            // 确保当前配置有效
            if (!string.IsNullOrEmpty(config.CurrentProfile))
            {
                var currentProfile = config.Profiles.FirstOrDefault(p => p.Id == config.CurrentProfile);
                if (currentProfile == null)
                {
                    config.CurrentProfile = string.Empty;
                }
            }

            // 如果没有当前配置且有配置列表，选择第一个
            if (string.IsNullOrEmpty(config.CurrentProfile) && config.Profiles.Count > 0)
            {
                config.CurrentProfile = config.Profiles[0].Id;
            }

            // 验证每个配置文件
            foreach (var profile in config.Profiles)
            {
                profile.KeyMappings ??= new();
                
                // 确保配置有ID
                if (string.IsNullOrEmpty(profile.Id))
                {
                    profile.Id = Guid.NewGuid().ToString();
                }
                
                // 为每个按键映射生成ID（如果没有）
                foreach (var mapping in profile.KeyMappings)
                {
                    if (string.IsNullOrEmpty(mapping.Id))
                    {
                        mapping.Id = Guid.NewGuid().ToString();
                    }
                    
                    mapping.Keys ??= new KeyCombination();
                    mapping.Audio ??= new AudioSettings();
                    
                    // 确保音量在合理范围
                    mapping.Audio.Volume = Math.Clamp(mapping.Audio.Volume, 0f, 1f);
                    
                    // 确保淡入淡出时间为正数
                    mapping.Audio.FadeInMs = Math.Max(0, mapping.Audio.FadeInMs);
                    mapping.Audio.FadeOutMs = Math.Max(0, mapping.Audio.FadeOutMs);
                }
            }
        }

        private static AppConfigV2 CreateDefaultConfig()
        {
            return new AppConfigV2
            {
                Global = new GlobalSettings(),
                Profiles = new List<ConfigProfile>(),
                CurrentProfile = string.Empty
            };
        }

        // 导出配置到指定文件
        public static bool ExportConfig(AppConfigV2 config, string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 从指定文件导入配置
        public static AppConfigV2? ImportConfig(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                
                var json = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<AppConfigV2>(json, JsonOptions);
                
                if (config != null)
                {
                    ValidateAndRepairConfig(config);
                }
                
                return config;
            }
            catch
            {
                return null;
            }
        }
    }
}
