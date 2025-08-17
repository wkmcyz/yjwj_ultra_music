using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

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
        public static bool ExportConfig(string filePath)
        {
            try
            {
                var config = Load();
                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置导出失败: {ex.Message}");
                return false;
            }
        }

        // 从指定文件导入配置
        public static bool ImportConfig(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                var json = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<AppConfigV2>(json, JsonOptions);
                
                if (config == null)
                    return false;

                // 验证和修复导入的配置
                ValidateAndRepairConfig(config);
                
                // 保存导入的配置
                Save(config);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置导入失败: {ex.Message}");
                return false;
            }
        }

        // 备份当前配置
        public static bool BackupConfig(string backupPath)
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    File.Copy(ConfigPath, backupPath, true);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置备份失败: {ex.Message}");
                return false;
            }
        }

        // 从备份恢复配置
        public static bool RestoreConfig(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                    return false;

                File.Copy(backupPath, ConfigPath, true);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置恢复失败: {ex.Message}");
                return false;
            }
        }

        // 导出配置包（包含音乐文件）
        public static bool ExportConfigPackage(string packagePath)
        {
            try
            {
                var config = Load();
                var tempDir = Path.Combine(Path.GetTempPath(), $"UltAssist_Export_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    // 保存配置文件
                    var configFile = Path.Combine(tempDir, "config.json");
                    var configCopy = CreateConfigCopyWithRelativePaths(config, tempDir);
                    var json = JsonSerializer.Serialize(configCopy, JsonOptions);
                    File.WriteAllText(configFile, json);

                    // 复制音乐文件
                    CopyAudioFiles(config, tempDir);

                    // 创建ZIP文件
                    if (File.Exists(packagePath))
                        File.Delete(packagePath);
                    
                    ZipFile.CreateFromDirectory(tempDir, packagePath);
                    return true;
                }
                finally
                {
                    // 清理临时目录
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置包导出失败: {ex.Message}");
                return false;
            }
        }

        // 导入配置包（包含音乐文件）
        public static bool ImportConfigPackage(string packagePath)
        {
            try
            {
                if (!File.Exists(packagePath))
                    return false;

                var tempDir = Path.Combine(Path.GetTempPath(), $"UltAssist_Import_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    // 解压ZIP文件
                    ZipFile.ExtractToDirectory(packagePath, tempDir);

                    var configFile = Path.Combine(tempDir, "config.json");
                    if (!File.Exists(configFile))
                        return false;

                    // 读取配置
                    var json = File.ReadAllText(configFile);
                    var config = JsonSerializer.Deserialize<AppConfigV2>(json, JsonOptions);
                    
                    if (config == null)
                        return false;

                    // 创建导入目录
                    var importAudioDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "imported_audio");
                    Directory.CreateDirectory(importAudioDir);

                    // 复制音乐文件并更新路径
                    UpdateAudioPathsAfterImport(config, tempDir, importAudioDir);

                    // 验证和修复配置
                    ValidateAndRepairConfig(config);
                    
                    // 保存配置
                    Save(config);
                    return true;
                }
                finally
                {
                    // 清理临时目录
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置包导入失败: {ex.Message}");
                return false;
            }
        }

        private static AppConfigV2 CreateConfigCopyWithRelativePaths(AppConfigV2 original, string exportDir)
        {
            // 创建配置的深拷贝并更新音乐文件路径为相对路径
            var json = JsonSerializer.Serialize(original, JsonOptions);
            var copy = JsonSerializer.Deserialize<AppConfigV2>(json, JsonOptions)!;

            // 更新所有音乐文件路径为相对路径
            foreach (var profile in copy.Profiles)
            {
                foreach (var mapping in profile.KeyMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.Audio.FilePath) && File.Exists(mapping.Audio.FilePath))
                    {
                        var fileName = Path.GetFileName(mapping.Audio.FilePath);
                        mapping.Audio.FilePath = Path.Combine("audio", fileName);
                    }
                }
            }

            return copy;
        }

        private static void CopyAudioFiles(AppConfigV2 config, string exportDir)
        {
            var audioDir = Path.Combine(exportDir, "audio");
            Directory.CreateDirectory(audioDir);

            var copiedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var profile in config.Profiles)
            {
                foreach (var mapping in profile.KeyMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.Audio.FilePath) && File.Exists(mapping.Audio.FilePath))
                    {
                        var fileName = Path.GetFileName(mapping.Audio.FilePath);
                        var destPath = Path.Combine(audioDir, fileName);

                        // 避免重复复制同名文件
                        if (!copiedFiles.Contains(fileName))
                        {
                            File.Copy(mapping.Audio.FilePath, destPath, true);
                            copiedFiles.Add(fileName);
                        }
                    }
                }
            }
        }

        private static void UpdateAudioPathsAfterImport(AppConfigV2 config, string tempDir, string importAudioDir)
        {
            var audioDir = Path.Combine(tempDir, "audio");
            if (!Directory.Exists(audioDir))
                return;

            foreach (var profile in config.Profiles)
            {
                foreach (var mapping in profile.KeyMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.Audio.FilePath))
                    {
                        var fileName = Path.GetFileName(mapping.Audio.FilePath);
                        var sourcePath = Path.Combine(audioDir, fileName);
                        var destPath = Path.Combine(importAudioDir, fileName);

                        if (File.Exists(sourcePath))
                        {
                            // 复制音乐文件到导入目录
                            File.Copy(sourcePath, destPath, true);
                            // 更新配置中的路径
                            mapping.Audio.FilePath = destPath;
                        }
                    }
                }
            }
        }
    }
}
