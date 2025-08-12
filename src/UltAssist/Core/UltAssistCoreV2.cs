using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;
using UltAssist.Audio;
using UltAssist.Config;
using UltAssist.Input;
using UltAssist.Services;

namespace UltAssist.Core
{
    public class UltAssistCoreV2 : IDisposable
    {
        private readonly AudioDeviceService _audioService;
        private readonly InputManagerV2 _inputManager;
        private AudioPlayerV2? _audioPlayer;
        
        // 配置
        private AppConfigV2 _config;
        
        // 事件
        public event Action<bool>? GlobalEnabledChanged;
        public event Action<bool>? GameWindowActiveChanged;
        public event Action<List<string>>? PlayingAudiosChanged;
        public event Action<string, DateTime>? LastKeyPressedChanged; // 按键名称, 时间

        // 属性
        public bool GlobalEnabled => _inputManager.GlobalEnabled;
        public bool IsGameWindowActive => _inputManager.IsGameWindowActive;
        public List<string> CurrentlyPlayingFiles => _audioPlayer?.CurrentlyPlayingFiles ?? new();
        public string CurrentHero => _config.CurrentHero;

        public UltAssistCoreV2()
        {
            _audioService = new AudioDeviceService();
            _inputManager = new InputManagerV2();
            _config = ConfigServiceV2.Load();

            // 订阅输入事件
            _inputManager.KeyCombinationTriggered += OnKeyCombinationTriggered;
            _inputManager.GlobalEnabledChanged += OnGlobalEnabledChanged;
            _inputManager.GameWindowActiveChanged += OnGameWindowActiveChanged;

            // 初始化音频播放器
            InitializeAudioPlayer();
            
            // 应用配置
            ApplyConfiguration();
        }

        public void LoadConfiguration()
        {
            _config = ConfigServiceV2.Load();
            ApplyConfiguration();
        }

        public void SaveConfiguration()
        {
            ConfigServiceV2.Save(_config);
        }

        public AppConfigV2 GetConfiguration() => _config;

        public void UpdateGlobalSettings(GlobalSettings settings)
        {
            _config.Global = settings;
            ApplyConfiguration();
            SaveConfiguration();
        }

        public void UpdateCurrentHero(string heroName)
        {
            if (_config.HeroConfigs.ContainsKey(heroName))
            {
                _config.CurrentHero = heroName;
                SaveConfiguration();
            }
        }

        public HeroConfigV2? GetCurrentHeroConfig()
        {
            return _config.HeroConfigs.TryGetValue(_config.CurrentHero, out var hero) ? hero : null;
        }

        public void UpdateHeroConfig(string heroName, HeroConfigV2 heroConfig)
        {
            _config.HeroConfigs[heroName] = heroConfig;
            SaveConfiguration();
        }

        public void AddKeyMapping(string heroName, KeyMapping mapping)
        {
            if (!_config.HeroConfigs.TryGetValue(heroName, out var heroConfig))
            {
                heroConfig = new HeroConfigV2 { Name = heroName };
                _config.HeroConfigs[heroName] = heroConfig;
            }

            // 检查按键冲突
            if (HasKeyConflict(heroName, mapping.Keys))
            {
                throw new InvalidOperationException($"按键组合 {mapping.Keys.ToDisplayString()} 已存在");
            }

            heroConfig.KeyMappings.Add(mapping);
            SaveConfiguration();
        }

        public void RemoveKeyMapping(string heroName, string mappingId)
        {
            if (_config.HeroConfigs.TryGetValue(heroName, out var heroConfig))
            {
                heroConfig.KeyMappings.RemoveAll(m => m.Id == mappingId);
                SaveConfiguration();
            }
        }

        public void UpdateKeyMapping(string heroName, KeyMapping mapping)
        {
            if (_config.HeroConfigs.TryGetValue(heroName, out var heroConfig))
            {
                var index = heroConfig.KeyMappings.FindIndex(m => m.Id == mapping.Id);
                if (index >= 0)
                {
                    // 检查按键冲突（排除自己）
                    var otherMappings = heroConfig.KeyMappings.Where(m => m.Id != mapping.Id);
                    if (otherMappings.Any(m => m.Keys.Equals(mapping.Keys)))
                    {
                        throw new InvalidOperationException($"按键组合 {mapping.Keys.ToDisplayString()} 已存在");
                    }

                    heroConfig.KeyMappings[index] = mapping;
                    SaveConfiguration();
                }
            }
        }

        public bool HasKeyConflict(string heroName, KeyCombination keys)
        {
            if (!_config.HeroConfigs.TryGetValue(heroName, out var heroConfig))
                return false;

            return heroConfig.KeyMappings.Any(m => m.Keys.Equals(keys));
        }

        public void TestPlayMapping(KeyMapping mapping)
        {
            if (_audioPlayer != null && !string.IsNullOrEmpty(mapping.Audio.FilePath))
            {
                _audioPlayer.PlayAudio(mapping.Keys, mapping.Audio);
            }
        }

        public void StopAllAudios()
        {
            _audioPlayer?.StopAllAudios(immediate: false);
        }

        public void StopAllAudiosImmediate()
        {
            _audioPlayer?.StopAllAudios(immediate: true);
        }

        private void InitializeAudioPlayer()
        {
            try
            {
                var headphone = _audioService.GetDeviceByIdOrDefault(_config.Global.HeadphoneDeviceId, DataFlow.Render);
                var virtualMic = _audioService.GetDeviceByIdOrDefault(_config.Global.VirtualMicDeviceId, DataFlow.Render);
                
                _audioPlayer?.Dispose();
                _audioPlayer = new AudioPlayerV2(headphone, virtualMic);
                _audioPlayer.PlayingAudiosChanged += OnPlayingAudiosChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"音频播放器初始化失败: {ex.Message}");
            }
        }

        private void ApplyConfiguration()
        {
            // 更新输入管理器设置
            _inputManager.UpdateSettings(_config.Global);
            
            // 重新初始化音频播放器（如果设备改变）
            InitializeAudioPlayer();
            
            // 应用临时默认麦设置
            if (_config.Global.TemporarilySetDefaultMic)
            {
                ApplyTempDefaultMic();
            }
        }

        private void ApplyTempDefaultMic()
        {
            try
            {
                var virtualMic = _audioService.GetDeviceByIdOrDefault(_config.Global.VirtualMicDeviceId, DataFlow.Render);
                var captureId = _audioService.FindLinkedCaptureId(virtualMic);
                if (!string.IsNullOrEmpty(captureId))
                {
                    _audioService.TrySetDefaultDevice(captureId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置默认麦克风失败: {ex.Message}");
            }
        }

        private void OnKeyCombinationTriggered(KeyCombination combination)
        {
            // 查找当前英雄的匹配映射
            var heroConfig = GetCurrentHeroConfig();
            if (heroConfig == null) return;

            // 按优先级排序：组合键优先于单键
            var matchingMappings = heroConfig.KeyMappings
                .Where(m => m.Keys.Equals(combination))
                .OrderByDescending(m => m.Keys.Keys.Count)
                .ToList();

            var mapping = matchingMappings.FirstOrDefault();
            if (mapping != null)
            {
                // 只有匹配到映射时才通知UI更新最后按键和播放音频
                LastKeyPressedChanged?.Invoke(combination.ToDisplayString(), DateTime.Now);
                
                if (_audioPlayer != null)
                {
                    _audioPlayer.PlayAudio(combination, mapping.Audio);
                }
            }
        }

        private void OnGlobalEnabledChanged(bool enabled)
        {
            _config.Global.GlobalListenerEnabled = enabled;
            SaveConfiguration();
            GlobalEnabledChanged?.Invoke(enabled);
        }

        private void OnGameWindowActiveChanged(bool isActive)
        {
            GameWindowActiveChanged?.Invoke(isActive);
        }

        private void OnPlayingAudiosChanged(List<string> playingFiles)
        {
            PlayingAudiosChanged?.Invoke(playingFiles);
        }

        public void Dispose()
        {
            _inputManager?.Dispose();
            _audioPlayer?.Dispose();
        }
    }
}
