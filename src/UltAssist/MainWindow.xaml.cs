using Microsoft.Win32;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using UltAssist.Config;
using UltAssist.Core;
using UltAssist.Input;
using UltAssist.Services;

namespace UltAssist
{
    public partial class MainWindow : Window
    {
        private AppConfig _config = null!;
        private UltStateMachine _stateMachine = null!;
        private GlobalHotkey? _hotkey;
        private AudioDeviceService _audioService = null!;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _config = ConfigService.Load();
            _audioService = new AudioDeviceService();
            PopulateDevices();
            PopulateHeroes();
            FadeInBox.Text = _config.FadeInMs.ToString();
            FadeOutBox.Text = _config.FadeOutMs.ToString();

            var hp = _audioService.GetDeviceByIdOrDefault(_config.HeadphoneDeviceId, DataFlow.Render);
            var vm = _audioService.GetDeviceByIdOrDefault(_config.VirtualMicDeviceId, DataFlow.Render);
            _stateMachine = new UltStateMachine(hp, vm, _config.FadeInMs, _config.FadeOutMs);
            _stateMachine.SetHero(CurrentHero());
            _stateMachine.PlayingStateChanged += playing => Dispatcher.Invoke(() =>
            {
                StatusText.Text = playing ? "Playing" : "Idle";
            });

            var interop = new WindowInteropHelper(this);
            _hotkey = new GlobalHotkey(interop.Handle, 0x56); // V
            _hotkey.Pressed += () => Dispatcher.Invoke(() =>
            {
                LastHotkeyText.Text = DateTime.Now.ToString("HH:mm:ss.fff");
                _stateMachine.OnHotkey();
            });
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _hotkey?.Dispose();
            _stateMachine?.Dispose();
        }

        private void PopulateDevices()
        {
            var renderDevices = _audioService.GetRenderDevices();
            HeadphoneCombo.ItemsSource = renderDevices;
            VirtualMicCombo.ItemsSource = renderDevices;

            if (!string.IsNullOrWhiteSpace(_config.HeadphoneDeviceId))
            {
                HeadphoneCombo.SelectedItem = renderDevices.FirstOrDefault(d => d.ID == _config.HeadphoneDeviceId) ?? renderDevices.FirstOrDefault();
            }
            else HeadphoneCombo.SelectedIndex = 0;

            if (!string.IsNullOrWhiteSpace(_config.VirtualMicDeviceId))
            {
                VirtualMicCombo.SelectedItem = renderDevices.FirstOrDefault(d => d.ID == _config.VirtualMicDeviceId) ?? renderDevices.FirstOrDefault();
            }
            else VirtualMicCombo.SelectedIndex = Math.Min(1, renderDevices.Count - 1);
        }

        private void PopulateHeroes()
        {
            if (_config.Heroes == null || _config.Heroes.Count == 0)
            {
                _config.Heroes = new List<HeroConfig> { new HeroConfig() };
                _config.CurrentHero = _config.Heroes[0].Hero;
            }
            HeroCombo.ItemsSource = _config.Heroes.Select(h => h.Hero).ToList();
            var idx = _config.Heroes.FindIndex(h => h.Hero == _config.CurrentHero);
            if (idx < 0) idx = 0;
            HeroCombo.SelectedIndex = idx;
            UpdateHeroFields(CurrentHero());
        }

        private HeroConfig CurrentHero() => _config.Heroes.First(h => h.Hero == (string)HeroCombo.SelectedItem);

        private void UpdateHeroFields(HeroConfig hero)
        {
            AudioPathBox.Text = hero.AudioPath ?? string.Empty;
            VolumeSlider.Value = hero.Volume;
            MaxMsBox.Text = hero.MaxDurationMs.ToString();
            LoopCheck.IsChecked = hero.Loop;
        }

        private void SaveGlobalBtn_Click(object sender, RoutedEventArgs e)
        {
            var hp = HeadphoneCombo.SelectedItem as MMDevice;
            var vm = VirtualMicCombo.SelectedItem as MMDevice;
            _config.HeadphoneDeviceId = hp?.ID ?? string.Empty;
            _config.VirtualMicDeviceId = vm?.ID ?? string.Empty;
            if (int.TryParse(FadeInBox.Text, out var fi)) _config.FadeInMs = fi;
            if (int.TryParse(FadeOutBox.Text, out var fo)) _config.FadeOutMs = fo;
            ConfigService.Save(_config);

            // 重新应用到状态机
            var hpDev = _audioService.GetDeviceByIdOrDefault(_config.HeadphoneDeviceId, DataFlow.Render);
            var vmDev = _audioService.GetDeviceByIdOrDefault(_config.VirtualMicDeviceId, DataFlow.Render);
            _stateMachine?.Dispose();
            _stateMachine = new UltStateMachine(hpDev, vmDev, _config.FadeInMs, _config.FadeOutMs);
            _stateMachine.SetHero(CurrentHero());

            MessageBox.Show("已保存全局设置", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveHeroBtn_Click(object sender, RoutedEventArgs e)
        {
            var hero = CurrentHero();
            hero.AudioPath = AudioPathBox.Text;
            hero.Volume = (float)VolumeSlider.Value;
            if (int.TryParse(MaxMsBox.Text, out var ms)) hero.MaxDurationMs = ms;
            hero.Loop = LoopCheck.IsChecked == true;
            _config.CurrentHero = hero.Hero;
            ConfigService.Save(_config);
            _stateMachine.SetHero(hero);
            MessageBox.Show("已保存英雄配置", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BrowseAudioBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "音频文件|*.mp3;*.wav;*.flac;*.m4a;*.ogg|所有文件|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                AudioPathBox.Text = dlg.FileName;
            }
        }

        private void AddHeroBtn_Click(object sender, RoutedEventArgs e)
        {
            var name = (NewHeroNameBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入英雄名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_config.Heroes.Any(h => h.Hero.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("该英雄已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _config.Heroes.Add(new HeroConfig { Hero = name });
            _config.CurrentHero = name;
            HeroCombo.ItemsSource = _config.Heroes.Select(h => h.Hero).ToList();
            HeroCombo.SelectedItem = name;
            ConfigService.Save(_config);
            UpdateHeroFields(CurrentHero());
            NewHeroNameBox.Text = string.Empty;
        }

        private void DeleteHeroBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_config.Heroes.Count <= 1)
            {
                MessageBox.Show("至少保留一个英雄", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var hero = CurrentHero();
            var idx = _config.Heroes.IndexOf(hero);
            _config.Heroes.Remove(hero);
            _config.CurrentHero = _config.Heroes[Math.Max(0, idx - 1)].Hero;
            HeroCombo.ItemsSource = _config.Heroes.Select(h => h.Hero).ToList();
            HeroCombo.SelectedItem = _config.CurrentHero;
            ConfigService.Save(_config);
            UpdateHeroFields(CurrentHero());
        }

        private void HeroCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (HeroCombo.SelectedItem == null || _config.Heroes == null) return;
            var hero = CurrentHero();
            _config.CurrentHero = hero.Hero;
            UpdateHeroFields(hero);
        }
    }
}

