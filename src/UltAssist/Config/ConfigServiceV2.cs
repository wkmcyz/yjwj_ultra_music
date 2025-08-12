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
                    var defaultConfig = DefaultsV2.CreateDefault();
                    Save(defaultConfig);
                    return defaultConfig;
                }

                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfigV2>(json, JsonOptions);
                
                if (config == null)
                {
                    return DefaultsV2.CreateDefault();
                }

                // 配置验证和修复
                ValidateAndRepairConfig(config);
                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置加载失败: {ex.Message}");
                return DefaultsV2.CreateDefault();
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
            config.HeroConfigs ??= new();

            // 确保有默认英雄
            if (config.HeroConfigs.Count == 0)
            {
                var defaultConfig = DefaultsV2.CreateDefault();
                config.HeroConfigs = defaultConfig.HeroConfigs;
                config.CurrentHero = defaultConfig.CurrentHero;
            }

            // 确保当前英雄有效
            if (string.IsNullOrEmpty(config.CurrentHero) || !config.HeroConfigs.ContainsKey(config.CurrentHero))
            {
                config.CurrentHero = config.HeroConfigs.Keys.First();
            }

            // 验证游戏进程名列表
            if (config.Global.GameProcessNames.Count == 0)
            {
                config.Global.GameProcessNames.Add("NarakaBladepoint.exe");
            }

            // 验证每个英雄配置
            foreach (var heroConfig in config.HeroConfigs.Values)
            {
                heroConfig.KeyMappings ??= new();
                
                // 为每个按键映射生成ID（如果没有）
                foreach (var mapping in heroConfig.KeyMappings)
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
