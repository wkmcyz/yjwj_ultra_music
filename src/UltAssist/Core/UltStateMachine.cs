using NAudio.CoreAudioApi;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using UltAssist.Audio;
using UltAssist.Config;
using UltAssist.Vision;
using Timer = System.Timers.Timer;

namespace UltAssist.Core
{
    public sealed class UltStateMachine : IDisposable
    {
        private readonly MMDevice headphone;
        private readonly MMDevice virtualMic;
        private DualOutputPlayer? player;
        private HeroConfig? hero;
        private readonly float fadeInMs;
        private readonly float fadeOutMs;
        private Timer? ttlTimer;
        public event Action<bool>? PlayingStateChanged; // true: playing, false: stopped
        public event Action<double, string?>? OpenMatchUpdated;
        public event Action<double, string?>? CloseMatchUpdated;

        private VisionConfig? visionConfig;
        private CancellationTokenSource? visionCts;

        public UltStateMachine(MMDevice hp, MMDevice vm, float fadeInMs, float fadeOutMs)
        {
            headphone = hp;
            virtualMic = vm;
            this.fadeInMs = fadeInMs;
            this.fadeOutMs = fadeOutMs;
        }

        public void SetHero(HeroConfig heroConfig)
        {
            hero = heroConfig;
        }

        public void SetVision(VisionConfig config)
        {
            visionConfig = config;
        }

        public void OnHotkey()
        {
            if (player != null)
            {
                Stop();
                return;
            }
            if (hero == null || string.IsNullOrWhiteSpace(hero.AudioPath) || !System.IO.File.Exists(hero.AudioPath))
            {
                return;
            }
            if (visionConfig == null || !visionConfig.Enabled)
            {
                StartPlayback();
                return;
            }

            visionCts?.Cancel();
            visionCts = new CancellationTokenSource();
            _ = ConfirmAndMaybeStartAsync(visionCts.Token);
        }

        public void Stop()
        {
            try { visionCts?.Cancel(); } catch { }
            ttlTimer?.Stop();
            ttlTimer?.Dispose();
            ttlTimer = null;
            player?.StopSmooth();
            player?.Dispose();
            player = null;
            PlayingStateChanged?.Invoke(false);
        }

        private void StartPlayback()
        {
            if (hero == null) return;
            player = new DualOutputPlayer(headphone, virtualMic, fadeInMs, fadeOutMs, hero.Loop);
            player.Start(hero.AudioPath, hero.Volume);
            StartTtl(hero.MaxDurationMs);
            PlayingStateChanged?.Invoke(true);

            if (visionConfig != null && visionConfig.Enabled)
            {
                visionCts?.Cancel();
                visionCts = new CancellationTokenSource();
                _ = MonitorCloseAsync(visionCts.Token);
            }
        }

        private async Task ConfirmAndMaybeStartAsync(CancellationToken token)
        {
            if (visionConfig == null) { StartPlayback(); return; }
            var roi = RoiToScreenRect(visionConfig.Roi);
            if (roi.IsEmpty) { StartPlayback(); return; }

            int required = Math.Max(1, visionConfig.RequiredConsecutiveFrames);
            int hits = 0;
            var deadline = DateTime.UtcNow.AddSeconds(2);

            while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                using var bmp = SafeCapture(roi);
                if (bmp != null)
                {
                    string? hit;
                    var openList = ResolveOpenTemplates();
                    double score = TemplateMatcher.MatchAny(bmp, openList, out hit);
                    OpenMatchUpdated?.Invoke(score, hit);
                    if (score >= visionConfig.OpenThreshold)
                    {
                        hits++;
                        if (hits >= required)
                        {
                            StartPlayback();
                            return;
                        }
                    }
                    else
                    {
                        hits = 0;
                    }
                }
                try { await Task.Delay(100, token); } catch { }
            }
        }

        private async Task MonitorCloseAsync(CancellationToken token)
        {
            if (visionConfig == null) return;
            var roi = RoiToScreenRect(visionConfig.Roi);
            if (roi.IsEmpty) return;
            int required = Math.Max(1, visionConfig.RequiredConsecutiveFrames);
            int hits = 0;

            while (!token.IsCancellationRequested && player != null)
            {
                using var bmp = SafeCapture(roi);
                if (bmp != null)
                {
                    string? hit;
                    var closeList = ResolveCloseTemplates();
                    double score = TemplateMatcher.MatchAny(bmp, closeList, out hit);
                    CloseMatchUpdated?.Invoke(score, hit);
                    if (score >= visionConfig.CloseThreshold)
                    {
                        hits++;
                        if (hits >= required)
                        {
                            Stop();
                            return;
                        }
                    }
                    else
                    {
                        hits = 0;
                    }
                }
                try { await Task.Delay(200, token); } catch { }
            }
        }

        private void StartTtl(int ms)
        {
            ttlTimer = new Timer(ms);
            ttlTimer.AutoReset = false;
            ttlTimer.Elapsed += (_, __) => Stop();
            ttlTimer.Start();
        }

        public void Dispose() => Stop();

        private static Rectangle RoiToScreenRect(RoiNormalized? roi)
        {
            if (roi == null) return Rectangle.Empty;
            var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
            int x = (int)(vs.Left + roi.X * vs.Width);
            int y = (int)(vs.Top + roi.Y * vs.Height);
            int w = (int)Math.Max(1, roi.W * vs.Width);
            int h = (int)Math.Max(1, roi.H * vs.Height);
            return new Rectangle(x, y, w, h);
        }

        private static Bitmap? SafeCapture(Rectangle roi)
        {
            try { return ScreenCaptureService.CaptureRoi(roi); } catch { return null; }
        }

        private System.Collections.Generic.IEnumerable<string> ResolveOpenTemplates()
        {
            if (hero != null && hero.OpenTemplates.Count > 0) return hero.OpenTemplates;
            if (visionConfig != null && visionConfig.OpenTemplates != null) return visionConfig.OpenTemplates;
            return System.Array.Empty<string>();
        }

        private System.Collections.Generic.IEnumerable<string> ResolveCloseTemplates()
        {
            if (hero != null && hero.CloseTemplates.Count > 0) return hero.CloseTemplates;
            if (visionConfig != null && visionConfig.CloseTemplates != null) return visionConfig.CloseTemplates;
            return System.Array.Empty<string>();
        }
    }
}

