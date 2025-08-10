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
                    return new AppConfig
                    {
                        Heroes = new() { new HeroConfig() },
                        CurrentHero = "Default"
                    };
                }
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig { Heroes = new() { new HeroConfig() } };
            }
            catch
            {
                return new AppConfig { Heroes = new() { new HeroConfig() } };
            }
        }

        public static void Save(AppConfig config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }
}

