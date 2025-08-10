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

            srcA = CreateReader(filePath, out srcADisposable);
            srcB = CreateReader(filePath, out srcBDisposable);
            srcA = Wrap(srcA);
            srcB = Wrap(srcB);

            volA = new VolumeSampleProvider(srcA) { Volume = 0f };
            volB = new VolumeSampleProvider(srcB) { Volume = 0f };

            outHeadphone = new WasapiOut(headphoneDevice, AudioClientShareMode.Shared, true, 30);
            outVirtualMic = new WasapiOut(virtualMicDevice, AudioClientShareMode.Shared, true, 30);
            outHeadphone.Init(volA);
            outVirtualMic.Init(volB);

            outHeadphone.Play();
            outVirtualMic.Play();
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

