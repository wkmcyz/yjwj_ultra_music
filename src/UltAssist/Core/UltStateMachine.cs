using NAudio.CoreAudioApi;
using System;
using UltAssist.Audio;
using UltAssist.Config;
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

            player = new DualOutputPlayer(headphone, virtualMic, fadeInMs, fadeOutMs, hero.Loop);
            player.Start(hero.AudioPath, hero.Volume);

            StartTtl(hero.MaxDurationMs);
            PlayingStateChanged?.Invoke(true);
        }

        public void Stop()
        {
            ttlTimer?.Stop();
            ttlTimer?.Dispose();
            ttlTimer = null;
            player?.StopSmooth();
            player?.Dispose();
            player = null;
            PlayingStateChanged?.Invoke(false);
        }

        private void StartTtl(int ms)
        {
            ttlTimer = new Timer(ms);
            ttlTimer.AutoReset = false;
            ttlTimer.Elapsed += (_, __) => Stop();
            ttlTimer.Start();
        }

        public void Dispose() => Stop();
    }
}

