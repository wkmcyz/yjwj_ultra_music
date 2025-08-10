using System;
using System.IO;
using System.Text.Json;

namespace UltAssist.Config
{
    public static class ConfigService
    {
        private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    return Defaults.BuildDefaultAppConfig();
                }
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg == null || cfg.Heroes == null || cfg.Heroes.Count == 0)
                {
                    return Defaults.BuildDefaultAppConfig();
                }
                // Merge new heroes detected under assets that are not yet in config
                try
                {
                    var assetsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
                    if (Directory.Exists(assetsRoot))
                    {
                        var existing = new HashSet<string>(cfg.Heroes.Select(h => h.Hero));
                        foreach (var dir in Directory.GetDirectories(assetsRoot))
                        {
                            var name = Path.GetFileName(dir);
                            if (!string.IsNullOrWhiteSpace(name) && !existing.Contains(name))
                            {
                                cfg.Heroes.Add(Defaults.BuildDefaultHeroConfig(name));
                            }
                        }
                    }
                }
                catch { }
                // 合并新增字段的默认值（向后兼容老配置）
                if (cfg.Vision == null) cfg.Vision = new VisionConfig();
                foreach (var h in cfg.Heroes)
                {
                    if (h.OpenTemplates == null) h.OpenTemplates = new();
                    if (h.CloseTemplates == null) h.CloseTemplates = new();
                    if (h.TemplatesDir == null) h.TemplatesDir = string.Empty;
                }
                return cfg;
            }
            catch
            {
                return Defaults.BuildDefaultAppConfig();
            }
        }

        public static void Save(AppConfig config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }
}

