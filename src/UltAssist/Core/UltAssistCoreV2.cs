using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;
using UltAssist.Audio;
using UltAssist.Config;
using UltAssist.Input;
using UltAssist.Logging;
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
        public bool IsGlobalEnabled => _inputManager.GlobalEnabled; // 兼容属性
        public bool IsGameWindowActive => _inputManager.IsGameWindowActive;
        public List<string> CurrentlyPlayingFiles => _audioPlayer?.CurrentlyPlayingFiles ?? new();
        public string CurrentProfile => _config.CurrentProfile;
        public AppConfigV2 Config => _config; // 公开配置访问

        public UltAssistCoreV2()
        {
            EventLogger.LogSystemInfo("初始化开始", "正在启动UltAssist Core v1.0.0");
            
            _audioService = new AudioDeviceService();
            _inputManager = new InputManagerV2();
            _config = ConfigServiceV2.Load();

            EventLogger.LogSystemInfo("配置加载", $"当前配置={_config.CurrentProfile}, 全局监听={_config.Global.GlobalListenerEnabled}");

            // 订阅输入事件
            _inputManager.KeyCombinationTriggered += OnKeyCombinationTriggered;
            _inputManager.GlobalEnabledChanged += OnGlobalEnabledChanged;
            _inputManager.GameWindowActiveChanged += OnGameWindowActiveChanged;

            // 初始化音频播放器
            InitializeAudioPlayer();
            
            // 应用配置
            ApplyConfiguration();
            
            EventLogger.LogSystemInfo("初始化完成", "UltAssist Core v1.0.0启动成功");
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

        public void SwitchProfile(string profileId)
        {
            var profile = _config.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile != null)
            {
                _config.CurrentProfile = profileId;
                SaveConfiguration();
            }
        }

        public ConfigProfile? GetCurrentProfile()
        {
            return _config.Profiles.FirstOrDefault(p => p.Id == _config.CurrentProfile);
        }

        public void UpdateProfile(ConfigProfile profile)
        {
            var existingProfile = _config.Profiles.FirstOrDefault(p => p.Id == profile.Id);
            if (existingProfile != null)
            {
                var index = _config.Profiles.IndexOf(existingProfile);
                _config.Profiles[index] = profile;
                SaveConfiguration();
            }
        }

        public void AddProfile(ConfigProfile profile)
        {
            _config.Profiles.Add(profile);
            if (string.IsNullOrEmpty(_config.CurrentProfile))
            {
                _config.CurrentProfile = profile.Id;
            }
            SaveConfiguration();
        }

        public void RemoveProfile(string profileId)
        {
            var profile = _config.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile != null)
            {
                _config.Profiles.Remove(profile);
                
                // 如果删除的是当前配置，切换到第一个可用配置
                if (_config.CurrentProfile == profileId)
                {
                    _config.CurrentProfile = _config.Profiles.Count > 0 ? _config.Profiles[0].Id : string.Empty;
                }
                
                SaveConfiguration();
            }
        }

        public void AddKeyMapping(string profileId, KeyMapping mapping)
        {
            var profile = _config.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile != null)
            {
                // 检查按键冲突
                if (HasKeyConflict(profileId, mapping.Keys))
                {
                    throw new InvalidOperationException($"按键组合 {mapping.Keys.ToDisplayString()} 已存在");
                }

                profile.KeyMappings.Add(mapping);
                SaveConfiguration();
            }
        }

        public void RemoveKeyMapping(string profileId, string mappingId)
        {
            var profile = _config.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile != null)
            {
                profile.KeyMappings.RemoveAll(m => m.Id == mappingId);
                SaveConfiguration();
            }
        }

        public void UpdateKeyMapping(string profileId, KeyMapping mapping)
        {
            var profile = _config.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile != null)
            {
                var index = profile.KeyMappings.FindIndex(m => m.Id == mapping.Id);
                if (index >= 0)
                {
                    // 检查按键冲突（排除自己）
                    var otherMappings = profile.KeyMappings.Where(m => m.Id != mapping.Id);
                    if (otherMappings.Any(m => m.Keys.Equals(mapping.Keys)))
                    {
                        throw new InvalidOperationException($"按键组合 {mapping.Keys.ToDisplayString()} 已存在");
                    }

                    profile.KeyMappings[index] = mapping;
                    SaveConfiguration();
                }
            }
        }

        public bool HasKeyConflict(string profileId, KeyCombination keys)
        {
            var profile = _config.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null)
                return false;

            return profile.KeyMappings.Any(m => m.Keys.Equals(keys));
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

        public void StopAudio(string keyId)
        {
            _audioPlayer?.StopAudio(keyId, immediate: false);
        }

        private void ToggleGlobalListening()
        {
            var newState = !_config.Global.GlobalListenerEnabled;
            _config.Global.GlobalListenerEnabled = newState;
            
            // 如果关闭监听，停止所有正在播放的音乐
            if (!newState && _audioPlayer != null)
            {
                EventLogger.LogEvent("AUDIO", "全局监听关闭", "停止所有播放中的音乐");
                _audioPlayer.StopAllAudios(immediate: false); // 使用淡出停止
            }
            
            // 立即应用状态变化
            _inputManager.SetGlobalEnabled(newState);
            
            // 保存配置
            ConfigServiceV2.Save(_config);
            
            // 通知UI状态变化
            GlobalEnabledChanged?.Invoke(newState);
            
            EventLogger.LogGlobalToggle(newState, "快捷键触发");
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
            var keyName = combination.ToDisplayString();
            
            // 检查是否是全局开关快捷键
            if (_config.Global.GlobalToggleHotkey != null && 
                combination.Equals(_config.Global.GlobalToggleHotkey))
            {
                // 切换全局监听状态
                ToggleGlobalListening();
                EventLogger.LogKeyPress(keyName, true, "GlobalToggle", "触发全局开关");
                return;
            }
            
            // 查找当前配置文件的匹配映射
            var profile = GetCurrentProfile();
            if (profile == null) 
            {
                EventLogger.LogKeyPress(keyName, false, _config.CurrentProfile, "配置文件不存在");
                return;
            }

            // 查找匹配的映射（支持精准匹配和包含匹配）
            var matchingMappings = profile.KeyMappings
                .Where(m => m.Keys.Matches(combination, m.ExactMatch))
                .OrderByDescending(m => m.Keys.Keys.Count) // 优先级：更多按键的组合优先
                .ThenByDescending(m => m.ExactMatch) // 相同按键数时，精准匹配优先
                .ToList();

            var mapping = matchingMappings.FirstOrDefault();
            if (mapping != null)
            {
                var mappingInfo = $"显示名={mapping.DisplayName}, 文件={mapping.Audio.FilePath}, 可打断={mapping.Audio.Interruptible}, 匹配模式={(mapping.ExactMatch ? "精准" : "包含")}";
                EventLogger.LogKeyPress(keyName, true, _config.CurrentProfile, mappingInfo);
                
                // 只有匹配到映射时才通知UI更新最后按键和播放音频
                LastKeyPressedChanged?.Invoke(keyName, DateTime.Now);
                
                if (_audioPlayer != null)
                {
                    try
                    {
                        EventLogger.LogEvent("AUDIO", "尝试播放", "按键={0}, 文件={1}", keyName, mapping.Audio.FilePath);
                        _audioPlayer.PlayAudio(combination, mapping.Audio);
                    }
                    catch (Exception ex)
                    {
                        EventLogger.LogError("AUDIO", "PlayAudio", ex);
                    }
                }
                else
                {
                    EventLogger.LogEvent("AUDIO", "播放失败", "AudioPlayer为null, 按键={0}", keyName);
                }
            }
            else
            {
                EventLogger.LogKeyPress(keyName, false, _config.CurrentProfile, $"无匹配映射 (共{profile.KeyMappings.Count}个映射)");
            }
        }

        private void OnGlobalEnabledChanged(bool enabled)
        {
            EventLogger.LogGlobalToggle(enabled, "按键触发");
            _config.Global.GlobalListenerEnabled = enabled;
            
            // 如果关闭监听，停止所有正在播放的音乐
            if (!enabled && _audioPlayer != null)
            {
                EventLogger.LogEvent("AUDIO", "全局监听关闭", "停止所有播放中的音乐 (来源: InputManager)");
                _audioPlayer.StopAllAudios(immediate: false); // 使用淡出停止
            }
            
            SaveConfiguration();
            GlobalEnabledChanged?.Invoke(enabled);
        }

        private void OnGameWindowActiveChanged(bool isActive)
        {
            EventLogger.LogGameWindow(isActive, string.Join(",", _config.Global.GameProcessNames));
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
