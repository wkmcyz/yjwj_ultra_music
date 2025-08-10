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
using UltAssist.Vision;

namespace UltAssist
{
    public partial class MainWindow : Window
    {
        private AppConfig _config = null!;
        private UltStateMachine _stateMachine = null!;
        private GlobalHotkey? _hotkey;
        private AudioDeviceService _audioService = null!;
        private System.Drawing.Rectangle? _roiRectPx;
        private RoiOverlayWindow? _overlay;

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
            // 初始化视觉配置 UI 状态
            VisionEnableCheck.IsChecked = _config.Vision?.Enabled == true;

            var hp = _audioService.GetDeviceByIdOrDefault(_config.HeadphoneDeviceId, DataFlow.Render);
            var vm = _audioService.GetDeviceByIdOrDefault(_config.VirtualMicDeviceId, DataFlow.Render);
            _stateMachine = new UltStateMachine(hp, vm, _config.FadeInMs, _config.FadeOutMs);
            _stateMachine.SetHero(CurrentHero());
            _stateMachine.SetVision(_config.Vision);
            _stateMachine.PlayingStateChanged += playing => Dispatcher.Invoke(() =>
            {
                StatusText.Text = playing ? "Playing" : "Idle";
            });
            _stateMachine.OpenMatchUpdated += (score, hit) => Dispatcher.Invoke(() =>
            {
                UpdateMatchScore(score, null);
            });
            _stateMachine.CloseMatchUpdated += (score, hit) => Dispatcher.Invoke(() =>
            {
                UpdateMatchScore(null, score);
            });

            var interop = new WindowInteropHelper(this);
            _hotkey = new GlobalHotkey(interop.Handle, 0x56); // V
            _hotkey.Pressed += () => Dispatcher.Invoke(() =>
            {
                LastHotkeyText.Text = DateTime.Now.ToString("HH:mm:ss.fff");
                // 即时读取当前 UI 的视觉开关并同步到状态机，避免未点击“保存视觉配置”时不生效
                _config.Vision.Enabled = VisionEnableCheck.IsChecked == true;
                _stateMachine.SetVision(_config.Vision);
                _stateMachine.OnHotkey();
            });

            _overlay = new RoiOverlayWindow();
            if (_config.Vision?.Enabled == true)
            {
                var rectInit = RoiToScreenRect();
                if (!rectInit.IsEmpty) _overlay.ShowAt(rectInit);
            }
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
            HeroTplDirBox.Text = hero.TemplatesDir ?? string.Empty;
            DefaultTplRootBox.Text = _config.Vision.DefaultTemplatesRoot ?? string.Empty;
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
            hero.TemplatesDir = HeroTplDirBox.Text?.Trim() ?? string.Empty;
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

        private void ResetHeroBtn_Click(object sender, RoutedEventArgs e)
        {
            var current = CurrentHero();
            var def = Defaults.BuildDefaultHeroConfig(current.Hero);
            // 替换当前英雄配置
            var idx = _config.Heroes.FindIndex(h => h.Hero == current.Hero);
            if (idx >= 0)
            {
                _config.Heroes[idx] = def;
                _config.CurrentHero = def.Hero;
                ConfigService.Save(_config);
                UpdateHeroFields(def);
                _stateMachine.SetHero(def);
                MessageBox.Show($"已恢复 {def.Hero} 的默认配置", "恢复默认", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void HeroCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (HeroCombo.SelectedItem == null || _config.Heroes == null) return;
            var hero = CurrentHero();
            _config.CurrentHero = hero.Hero;
            UpdateHeroFields(hero);
        }

        private void SelectRoiBtn_Click(object sender, RoutedEventArgs e)
        {
            var selector = new RoiSelectorWindow();
            if (selector.ShowDialog() == true)
            {
                _roiRectPx = selector.SelectedRectPixels;
                var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
                _config.Vision.Roi = new RoiNormalized
                {
                    X = (float)((_roiRectPx.Value.X - vs.Left) / (float)vs.Width),
                    Y = (float)((_roiRectPx.Value.Y - vs.Top) / (float)vs.Height),
                    W = (float)(_roiRectPx.Value.Width / (float)vs.Width),
                    H = (float)(_roiRectPx.Value.Height / (float)vs.Height)
                };
                _overlay?.ShowAt(_roiRectPx.Value);
            }
        }

        private System.Drawing.Rectangle RoiToScreenRect()
        {
            var vs = System.Windows.Forms.SystemInformation.VirtualScreen; // 全虚拟屏像素
            var roi = _config.Vision.Roi;
            if (roi == null) return System.Drawing.Rectangle.Empty;
            int x = (int)(vs.Left + roi.X * vs.Width);
            int y = (int)(vs.Top + roi.Y * vs.Height);
            int w = (int)Math.Max(1, roi.W * vs.Width);
            int h = (int)Math.Max(1, roi.H * vs.Height);
            return new System.Drawing.Rectangle(x, y, w, h);
        }

        private void CaptureOpenTemplateBtn_Click(object sender, RoutedEventArgs e)
        {
            var rect = RoiToScreenRect();
            if (rect == System.Drawing.Rectangle.Empty)
            {
                MessageBox.Show("请先框选 ROI", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            using var bmp = ScreenCaptureService.CaptureRoi(rect);
            var name = $"open_{DateTime.Now:yyyyMMdd_HHmmss}";
            var file = TemplateStore.Save(bmp, name);
            _config.Vision.OpenTemplates.Add(file);
        }

        private void CaptureCloseTemplateBtn_Click(object sender, RoutedEventArgs e)
        {
            var rect = RoiToScreenRect();
            if (rect == System.Drawing.Rectangle.Empty)
            {
                MessageBox.Show("请先框选 ROI", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            using var bmp = ScreenCaptureService.CaptureRoi(rect);
            var name = $"close_{DateTime.Now:yyyyMMdd_HHmmss}";
            var file = TemplateStore.Save(bmp, name);
            _config.Vision.CloseTemplates.Add(file);
        }

        private void SaveVisionBtn_Click(object sender, RoutedEventArgs e)
        {
            _config.Vision.Enabled = VisionEnableCheck.IsChecked == true;
            _config.Vision.DefaultTemplatesRoot = DefaultTplRootBox.Text?.Trim() ?? string.Empty;
            ConfigService.Save(_config);
            MessageBox.Show("已保存视觉配置", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
            if (_config.Vision.Enabled)
            {
                var rect = RoiToScreenRect();
                if (!rect.IsEmpty) _overlay?.ShowAt(rect);
                _stateMachine.SetVision(_config.Vision);
            }
            else
            {
                _overlay?.Hide();
            }
        }

        private void BrowseTplRootBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DefaultTplRootBox.Text = dlg.SelectedPath;
            }
        }

        private void ApplyHeroTplDirBtn_Click(object sender, RoutedEventArgs e)
        {
            var hero = CurrentHero();
            hero.TemplatesDir = HeroTplDirBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(hero.TemplatesDir) && System.IO.Directory.Exists(hero.TemplatesDir))
            {
                // 自动加载该目录下的 png 作为模板（一次性覆盖）
                hero.OpenTemplates = System.IO.Directory.GetFiles(hero.TemplatesDir, "open_*.png").ToList();
                hero.CloseTemplates = System.IO.Directory.GetFiles(hero.TemplatesDir, "close_*.png").ToList();
                _stateMachine.SetHero(hero);
                ConfigService.Save(_config);
                MessageBox.Show($"已为 {hero.Hero} 应用模板目录，共加载开大{hero.OpenTemplates.Count}张、收大{hero.CloseTemplates.Count}张。", "模板", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("目录不存在", "模板", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private double _lastOpenScore = 0, _lastCloseScore = 0;
        private void UpdateMatchScore(double? open, double? close)
        {
            if (open.HasValue) _lastOpenScore = open.Value;
            if (close.HasValue) _lastCloseScore = close.Value;
            MatchScoreText.Text = $"{_lastOpenScore:F2} / {_lastCloseScore:F2}";
        }

        private VisionPreviewWindow? _preview;
        private void OpenPreviewBtn_Click(object sender, RoutedEventArgs e)
        {
            var rect = RoiToScreenRect();
            if (rect == System.Drawing.Rectangle.Empty)
            {
                MessageBox.Show("ROI 为空，请先框选", "预览", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _preview?.Close();
            _preview = new VisionPreviewWindow(
                captureRoi: () => ScreenCaptureService.CaptureRoi(rect),
                getScores: () => (_lastOpenScore, _lastCloseScore),
                getTemplates: () =>
                {
                    try
                    {
                        var openPath = (_config.Heroes.First(h => h.Hero == _config.CurrentHero).OpenTemplates).FirstOrDefault(p => System.IO.File.Exists(p))
                                       ?? _config.Vision.OpenTemplates.FirstOrDefault(p => System.IO.File.Exists(p));
                        var closePath = (_config.Heroes.First(h => h.Hero == _config.CurrentHero).CloseTemplates).FirstOrDefault(p => System.IO.File.Exists(p))
                                        ?? _config.Vision.CloseTemplates.FirstOrDefault(p => System.IO.File.Exists(p));
                        System.Drawing.Bitmap? toBmp(string? p) => p != null ? new System.Drawing.Bitmap(p) : null;
                        return (toBmp(openPath), toBmp(closePath));
                    }
                    catch { return (null, null); }
                }
            );
            _preview.Show();
        }

        private void DumpVisionBtn_Click(object sender, RoutedEventArgs e)
        {
            var roi = RoiToScreenRect();
            if (roi == System.Drawing.Rectangle.Empty)
            {
                MessageBox.Show("ROI 为空，请先框选", "调试", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                using var bmp = ScreenCaptureService.CaptureRoi(roi);
                var dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_dump");
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                var roiPath = System.IO.Path.Combine(dir, $"roi_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                bmp.Save(roiPath);

                // 复制当前模板文件清单到调试目录（不改变原模板）
                var openList = string.Join("\n", _config.Vision.OpenTemplates);
                var closeList = string.Join("\n", _config.Vision.CloseTemplates);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "open_templates.txt"), openList);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "close_templates.txt"), closeList);

                MessageBox.Show($"已导出 ROI 截图到:\n{roiPath}\n并列出模板清单到 debug_dump 目录", "调试", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "调试", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

