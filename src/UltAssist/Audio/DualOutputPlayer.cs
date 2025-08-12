using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Threading.Tasks;

namespace UltAssist.Audio
{
    public sealed class DualOutputPlayer : IDisposable
    {
        private readonly MMDevice headphoneDevice;
        private readonly MMDevice virtualMicDevice;
        private WasapiOut? outHeadphone;
        private WasapiOut? outVirtualMic;
        private VolumeSampleProvider? volA, volB;
        private readonly float fadeInMs;
        private readonly float fadeOutMs;
        private readonly bool loop;
        private ISampleProvider? srcA, srcB;
        private IDisposable? srcADisposable, srcBDisposable;

        public DualOutputPlayer(MMDevice headphone, MMDevice virtualMic, float fadeInMs = 120f, float fadeOutMs = 150f, bool loop = false)
        {
            headphoneDevice = headphone;
            virtualMicDevice = virtualMic;
            this.fadeInMs = fadeInMs;
            this.fadeOutMs = fadeOutMs;
            this.loop = loop;
        }

        public void Start(string filePath, float volume)
        {
            Stop();

            // 为两个分支各自创建独立 reader，避免拉流竞争
            var readerA = CreateReader(filePath, out srcADisposable);
            var readerB = CreateReader(filePath, out srcBDisposable);

            // 适配至各自设备的混音格式（采样率/声道）
            srcA = AdaptToDeviceFormat(readerA, headphoneDevice);
            srcB = AdaptToDeviceFormat(readerB, virtualMicDevice);

            // 动态处理包络和限制
            srcA = Wrap(srcA);
            srcB = Wrap(srcB);

            volA = new VolumeSampleProvider(srcA) { Volume = 0f };
            volB = new VolumeSampleProvider(srcB) { Volume = 0f };

            // 独立启动两个输出，分别捕获异常，确保其中一个失败不影响另一个
            try
            {
                outHeadphone = new WasapiOut(headphoneDevice, AudioClientShareMode.Shared, true, 30);
                outHeadphone.Init(volA);
                outHeadphone.Play();
            }
            catch { TryDispose(ref outHeadphone); }

            try
            {
                // 某些虚拟声卡对事件驱动不友好，改为非事件驱动并提高缓冲
                outVirtualMic = new WasapiOut(virtualMicDevice, AudioClientShareMode.Shared, false, 100);
                outVirtualMic.Init(volB);
                outVirtualMic.Play();
            }
            catch { TryDispose(ref outVirtualMic); }

            _ = FadeTo(volume, fadeInMs);
        }

        public async void StopSmooth()
        {
            await FadeTo(0f, fadeOutMs);
            Stop();
        }

        public void Stop()
        {
            outHeadphone?.Stop();
            outVirtualMic?.Stop();
            outHeadphone?.Dispose();
            outVirtualMic?.Dispose();
            outHeadphone = null;
            outVirtualMic = null;
            volA = null;
            volB = null;
            srcA = null;
            srcB = null;
            srcADisposable?.Dispose();
            srcBDisposable?.Dispose();
            srcADisposable = null;
            srcBDisposable = null;
        }

        private ISampleProvider CreateReader(string file, out IDisposable disposable)
        {
            var reader = new AudioFileReader(file);
            if (!loop)
            {
                disposable = reader;
                return reader;
            }
            var looping = new LoopAudioFileReader(reader);
            disposable = looping;
            return looping;
        }

        private ISampleProvider AdaptToDeviceFormat(ISampleProvider source, MMDevice device)
        {
            var mix = device.AudioClient.MixFormat;
            var result = source;

            // 采样率重采样
            if (result.WaveFormat.SampleRate != mix.SampleRate)
            {
                result = new WdlResamplingSampleProvider(result, mix.SampleRate);
            }

            // 声道数转换（常见：1<->2）
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
                // 其他声道差异暂不强制转换，交给驱动做下混/上混
            }
            return result;
        }

        private static void TryDispose(ref WasapiOut? output)
        {
            try { output?.Stop(); } catch { }
            try { output?.Dispose(); } catch { }
            output = null;
        }

        private ISampleProvider Wrap(ISampleProvider src) => new LimiterSampleProvider(src);

        private async Task FadeTo(float target, float ms)
        {
            if (volA == null || volB == null) return;
            const int steps = 24;
            float dt = ms / steps;
            float startA = volA.Volume;
            float startB = volB.Volume;
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 1) / (float)steps;
                float vA = startA + (target - startA) * t;
                float vB = startB + (target - startB) * t;
                if (volA != null) volA.Volume = vA;
                if (volB != null) volB.Volume = vB;
                await Task.Delay((int)dt);
            }
        }

        public void Dispose() => Stop();

        private sealed class LoopAudioFileReader : ISampleProvider, IDisposable
        {
            private readonly AudioFileReader reader;
            public LoopAudioFileReader(AudioFileReader r)
            {
                reader = r;
                WaveFormat = r.WaveFormat;
            }
            public WaveFormat WaveFormat { get; }
            public int Read(float[] buffer, int offset, int count)
            {
                int total = 0;
                while (total < count)
                {
                    int read = reader.Read(buffer, offset + total, count - total);
                    if (read == 0)
                    {
                        reader.Position = 0;
                        continue;
                    }
                    total += read;
                }
                return total;
            }
            public void Dispose() => reader.Dispose();
        }

        private sealed class LimiterSampleProvider : ISampleProvider
        {
            private readonly ISampleProvider source;
            public LimiterSampleProvider(ISampleProvider src) { source = src; }
            public WaveFormat WaveFormat => source.WaveFormat;
            private const float Threshold = 0.9f;
            public int Read(float[] buffer, int offset, int count)
            {
                int n = source.Read(buffer, offset, count);
                for (int i = 0; i < n; i++)
                {
                    float s = buffer[offset + i];
                    if (s > Threshold) s = Threshold + (s - Threshold) * 0.2f;
                    if (s < -Threshold) s = -Threshold + (s + Threshold) * 0.2f;
                    buffer[offset + i] = s;
                }
                return n;
            }
        }
    }
}

