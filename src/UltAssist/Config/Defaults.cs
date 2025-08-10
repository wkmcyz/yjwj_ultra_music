using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UltAssist.Config
{
    public static class Defaults
    {
        public static readonly string[] HeroNames = new[]
        {
            "季沧海","迦南","妖刀姬","武田信忠","顾清寒","胡桃","无尘","宁红夜","特木尔","崔三娘","天海","玉玲珑","席拉","岳山"
        };

        public static AppConfig BuildDefaultAppConfig()
        {
            var app = new AppConfig
            {
                Vision = new VisionConfig
                {
                    Enabled = false,
                    DefaultTemplatesRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets")
                },
                Heroes = new List<HeroConfig>(),
                CurrentHero = HeroNames.First()
            };
            // Gather heroes from built-in list and assets directory
            var allHeroes = new HashSet<string>(HeroNames);
            try
            {
                var assetsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
                if (Directory.Exists(assetsRoot))
                {
                    foreach (var dir in Directory.GetDirectories(assetsRoot))
                    {
                        var name = Path.GetFileName(dir);
                        if (!string.IsNullOrWhiteSpace(name)) allHeroes.Add(name);
                    }
                }
            }
            catch { }

            foreach (var name in allHeroes)
            {
                app.Heroes.Add(BuildDefaultHeroConfig(name));
            }
            return app;
        }

        public static HeroConfig BuildDefaultHeroConfig(string heroName)
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", heroName);
            string templatesDir = Path.Combine(baseDir, "templates");
            var hero = new HeroConfig
            {
                Hero = heroName,
                AudioPath = Path.Combine(baseDir, "ult.mp3"),
                Volume = 0.85f,
                MaxDurationMs = 12000,
                Loop = false,
                TemplatesDir = templatesDir,
                OpenTemplates = new List<string>(),
                CloseTemplates = new List<string>()
            };

            try
            {
                if (Directory.Exists(templatesDir))
                {
                    hero.OpenTemplates = Directory.GetFiles(templatesDir, "open_*.png").ToList();
                    hero.CloseTemplates = Directory.GetFiles(templatesDir, "close_*.png").ToList();
                }
            }
            catch { }

            return hero;
        }
    }
}


