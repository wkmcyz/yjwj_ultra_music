using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UltAssist.Config;

namespace UltAssist.Audio
{
    public class AudioPlayerV2 : IDisposable
    {
        private readonly MMDevice _headphoneDevice;
        private readonly MMDevice _virtualMicDevice;
        
        // 当前播放的音频实例
        private readonly ConcurrentDictionary<string, PlayingAudio> _playingAudios = new();
        
        // 事件
        public event Action<List<string>>? PlayingAudiosChanged; // 当前播放的音频文件名列表变化

        public List<string> CurrentlyPlayingFiles => 
            _playingAudios.Values.Select(p => Path.GetFileName(p.AudioSettings.FilePath)).ToList();

        public AudioPlayerV2(MMDevice headphone, MMDevice virtualMic)
        {
            _headphoneDevice = headphone;
            _virtualMicDevice = virtualMic;
        }

        public void PlayAudio(KeyCombination triggeredKey, AudioSettings audioSettings)
        {
            if (string.IsNullOrEmpty(audioSettings.FilePath) || !File.Exists(audioSettings.FilePath))
                return;

            var keyId = triggeredKey.ToDisplayString();

            // 检查重复按键：如果同一按键已在播放，先停止之前的
            if (_playingAudios.TryGetValue(keyId, out var existingAudio))
            {
                StopAudio(keyId, immediate: true); // 立即停止，不淡出
            }

            // 检查可打断性：停止所有可打断的音频
            var interruptibleKeys = _playingAudios
                .Where(kvp => kvp.Value.AudioSettings.Interruptible)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var interruptibleKey in interruptibleKeys)
            {
                StopAudio(interruptibleKey, immediate: true);
            }

            // 开始播放新音频
            try
            {
                var playingAudio = new PlayingAudio(
                    _headphoneDevice, 
                    _virtualMicDevice, 
                    audioSettings, 
                    () => OnAudioCompleted(keyId)
                );
                
                if (_playingAudios.TryAdd(keyId, playingAudio))
                {
                    playingAudio.Start();
                    NotifyPlayingAudiosChanged();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"播放音频失败: {ex.Message}");
            }
        }

        public void StopAudio(string keyId, bool immediate = false)
        {
            if (_playingAudios.TryRemove(keyId, out var audio))
            {
                if (immediate)
                {
                    audio.Stop();
                }
                else
                {
                    audio.StopWithFadeOut();
                }
                NotifyPlayingAudiosChanged();
            }
        }

        public void StopAllAudios(bool immediate = false)
        {
            var keys = _playingAudios.Keys.ToList();
            foreach (var key in keys)
            {
                StopAudio(key, immediate);
            }
        }

        private void OnAudioCompleted(string keyId)
        {
            if (_playingAudios.TryRemove(keyId, out var audio))
            {
                audio.Dispose();
                NotifyPlayingAudiosChanged();
            }
        }

        private void NotifyPlayingAudiosChanged()
        {
            PlayingAudiosChanged?.Invoke(CurrentlyPlayingFiles);
        }

        public void UpdateDevices(MMDevice headphone, MMDevice virtualMic)
        {
            // 停止所有当前播放，更新设备后需要重新初始化
            StopAllAudios(immediate: true);
            
            // 注意：实际使用中可能需要重新创建 AudioPlayerV2 实例
            // 这里只是标记设备更新，实际设备切换会在重新创建时生效
        }

        public void Dispose()
        {
            StopAllAudios(immediate: true);
        }
    }

    // 单个音频播放实例
    internal class PlayingAudio : IDisposable
    {
        private readonly MMDevice _headphoneDevice;
        private readonly MMDevice _virtualMicDevice;
        private readonly AudioSettings _audioSettings;
        private readonly Action _onCompleted;

        private WasapiOut? _outHeadphone;
        private WasapiOut? _outVirtualMic;
        private VolumeSampleProvider? _volHeadphone;
        private VolumeSampleProvider? _volVirtualMic;
        private ISampleProvider? _srcHeadphone;
        private ISampleProvider? _srcVirtualMic;
        private IDisposable? _srcHeadphoneDisposable;
        private IDisposable? _srcVirtualMicDisposable;

        public AudioSettings AudioSettings => _audioSettings;

        public PlayingAudio(MMDevice headphone, MMDevice virtualMic, AudioSettings audioSettings, Action onCompleted)
        {
            _headphoneDevice = headphone;
            _virtualMicDevice = virtualMic;
            _audioSettings = audioSettings;
            _onCompleted = onCompleted;
        }

        public void Start()
        {
            try
            {
                // 为两个输出创建独立的音频源
                _srcHeadphone = CreateAudioSource(out _srcHeadphoneDisposable);
                _srcVirtualMic = CreateAudioSource(out _srcVirtualMicDisposable);

                // 适配设备格式
                _srcHeadphone = AdaptToDeviceFormat(_srcHeadphone, _headphoneDevice);
                _srcVirtualMic = AdaptToDeviceFormat(_srcVirtualMic, _virtualMicDevice);

                // 添加音量控制
                _volHeadphone = new VolumeSampleProvider(_srcHeadphone) { Volume = 0f };
                _volVirtualMic = new VolumeSampleProvider(_srcVirtualMic) { Volume = 0f };

                // 启动输出
                StartOutput(_headphoneDevice, _volHeadphone, out _outHeadphone);
                StartOutput(_virtualMicDevice, _volVirtualMic, out _outVirtualMic);

                // 淡入效果
                _ = FadeIn();

                // 如果不是循环播放，设置播放完成监听
                if (!_audioSettings.Loop)
                {
                    _ = MonitorPlaybackCompletion();
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void StopWithFadeOut()
        {
            _ = FadeOutAndStop();
        }

        public void Stop()
        {
            Dispose();
        }

        private ISampleProvider CreateAudioSource(out IDisposable disposable)
        {
            var reader = new AudioFileReader(_audioSettings.FilePath);
            
            if (!_audioSettings.Loop)
            {
                disposable = reader;
                return reader;
            }

            var looping = new LoopingAudioFileReader(reader);
            disposable = looping;
            return looping;
        }

        private static ISampleProvider AdaptToDeviceFormat(ISampleProvider source, MMDevice device)
        {
            var mix = device.AudioClient.MixFormat;
            var result = source;

            // 重采样
            if (result.WaveFormat.SampleRate != mix.SampleRate)
            {
                result = new WdlResamplingSampleProvider(result, mix.SampleRate);
            }

            // 声道转换
            if (result.WaveFormat.Channels != mix.Channels)
            {
                if (result.WaveFormat.Channels == 1 && mix.Channels == 2)
                {
                    result = new MonoToStereoSampleProvider(result);
                }
                else if (result.WaveFormat.Channels == 2 && mix.Channels == 1)
                {
                    result = new StereoToMonoSampleProvider(result);
                }
            }

            // 添加限幅器
            return new LimiterSampleProvider(result);
        }

        private static void StartOutput(MMDevice device, ISampleProvider source, out WasapiOut? output)
        {
            try
            {
                output = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);
                output.Init(source);
                output.Play();
            }
            catch
            {
                output = null;
            }
        }

        private async Task FadeIn()
        {
            if (_volHeadphone == null || _volVirtualMic == null) return;

            const int steps = 20;
            var stepDelay = _audioSettings.FadeInMs / steps;
            var volumeStep = _audioSettings.Volume / steps;

            for (int i = 1; i <= steps; i++)
            {
                var volume = volumeStep * i;
                if (_volHeadphone != null) _volHeadphone.Volume = volume;
                if (_volVirtualMic != null) _volVirtualMic.Volume = volume;
                
                await Task.Delay(stepDelay);
            }
        }

        private async Task FadeOutAndStop()
        {
            if (_volHeadphone == null || _volVirtualMic == null) return;

            const int steps = 15;
            var stepDelay = _audioSettings.FadeOutMs / steps;
            var startVolume = _audioSettings.Volume;
            var volumeStep = startVolume / steps;

            for (int i = 1; i <= steps; i++)
            {
                var volume = startVolume - (volumeStep * i);
                if (_volHeadphone != null) _volHeadphone.Volume = Math.Max(0, volume);
                if (_volVirtualMic != null) _volVirtualMic.Volume = Math.Max(0, volume);
                
                await Task.Delay(stepDelay);
            }

            Dispose();
            _onCompleted?.Invoke();
        }

        private async Task MonitorPlaybackCompletion()
        {
            // 等待播放完成（非循环模式）
            while (_outHeadphone?.PlaybackState == PlaybackState.Playing || 
                   _outVirtualMic?.PlaybackState == PlaybackState.Playing)
            {
                await Task.Delay(100);
            }

            _onCompleted?.Invoke();
        }

        public void Dispose()
        {
            try { _outHeadphone?.Stop(); } catch { }
            try { _outVirtualMic?.Stop(); } catch { }
            try { _outHeadphone?.Dispose(); } catch { }
            try { _outVirtualMic?.Dispose(); } catch { }
            try { _srcHeadphoneDisposable?.Dispose(); } catch { }
            try { _srcVirtualMicDisposable?.Dispose(); } catch { }

            _outHeadphone = null;
            _outVirtualMic = null;
            _volHeadphone = null;
            _volVirtualMic = null;
            _srcHeadphone = null;
            _srcVirtualMic = null;
            _srcHeadphoneDisposable = null;
            _srcVirtualMicDisposable = null;
        }
    }

    // 循环音频读取器
    internal class LoopingAudioFileReader : ISampleProvider, IDisposable
    {
        private readonly AudioFileReader _reader;

        public LoopingAudioFileReader(AudioFileReader reader)
        {
            _reader = reader;
            WaveFormat = reader.WaveFormat;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = _reader.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    _reader.Position = 0; // 循环到开头
                    continue;
                }
                totalRead += read;
            }
            return totalRead;
        }

        public void Dispose() => _reader.Dispose();
    }

    // 音频限幅器（防止炸麦）
    internal class LimiterSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private const float Threshold = 0.95f;

        public LimiterSampleProvider(ISampleProvider source)
        {
            _source = source;
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            
            for (int i = 0; i < read; i++)
            {
                var sample = buffer[offset + i];
                if (sample > Threshold)
                    buffer[offset + i] = Threshold + (sample - Threshold) * 0.1f;
                else if (sample < -Threshold)
                    buffer[offset + i] = -Threshold + (sample + Threshold) * 0.1f;
            }

            return read;
        }
    }
}
