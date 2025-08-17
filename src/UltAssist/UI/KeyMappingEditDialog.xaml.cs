using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using UltAssist.Config;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace UltAssist
{
    public partial class KeyMappingEditDialog : Window
    {
        private KeyMapping _keyMapping;
        private readonly bool _isNewMapping;
        private WaveOutEvent? _previewPlayer;
        private AudioFileReader? _previewReader;

        public KeyMapping KeyMapping => _keyMapping;

        // 创建新映射
        public KeyMappingEditDialog(KeyCombination keyCombination)
        {
            InitializeComponent();
            
            _isNewMapping = true;
            _keyMapping = new KeyMapping
            {
                Keys = keyCombination,
                Audio = new AudioSettings()
            };
            
            InitializeUI();
        }

        // 编辑现有映射
        public KeyMappingEditDialog(KeyMapping existingMapping)
        {
            InitializeComponent();
            
            _isNewMapping = false;
            _keyMapping = new KeyMapping
            {
                Id = existingMapping.Id,
                Keys = existingMapping.Keys,
                DisplayName = existingMapping.DisplayName,
                Audio = new AudioSettings
                {
                    FilePath = existingMapping.Audio.FilePath,
                    Volume = existingMapping.Audio.Volume,
                    FadeInMs = existingMapping.Audio.FadeInMs,
                    FadeOutMs = existingMapping.Audio.FadeOutMs,
                    Loop = existingMapping.Audio.Loop,
                    Interruptible = existingMapping.Audio.Interruptible
                }
            };
            
            InitializeUI();
        }

        private void InitializeUI()
        {
            // 显示按键组合
            KeyDisplayText.Text = _keyMapping.Keys.ToDisplayString();
            Title = _isNewMapping ? "添加按键映射" : "编辑按键映射";

            // 加载当前设置
            DisplayNameBox.Text = _keyMapping.DisplayName;
            ExactMatchRadio.IsChecked = _keyMapping.ExactMatch;
            ContainsMatchRadio.IsChecked = !_keyMapping.ExactMatch;
            AudioFileBox.Text = _keyMapping.Audio.FilePath;
            VolumeSlider.Value = _keyMapping.Audio.Volume;
            FadeInBox.Text = _keyMapping.Audio.FadeInMs.ToString();
            FadeOutBox.Text = _keyMapping.Audio.FadeOutMs.ToString();
            InterruptibleCheck.IsChecked = _keyMapping.Audio.Interruptible;

            // 设置新的配置项
            StopOnRepeatRadio.IsChecked = _keyMapping.Audio.RepeatBehavior == RepeatBehavior.Stop;
            RestartOnRepeatRadio.IsChecked = _keyMapping.Audio.RepeatBehavior == RepeatBehavior.Restart;
            
            DefaultDurationRadio.IsChecked = _keyMapping.Audio.DurationMode == DurationMode.Default;
            CustomDurationRadio.IsChecked = _keyMapping.Audio.DurationMode == DurationMode.Custom;
            CustomDurationBox.Text = _keyMapping.Audio.CustomDurationSeconds.ToString();
            
            UpdateCustomDurationPanelVisibility();

            // 绑定事件
            VolumeSlider.ValueChanged += (s, e) =>
            {
                VolumeText.Text = $"{VolumeSlider.Value:P0}";
            };
            
            // 初始化音量显示
            VolumeText.Text = $"{VolumeSlider.Value:P0}";
            
            // 验证保存按钮状态
            UpdateSaveButtonState();
        }

        private void UpdateSaveButtonState()
        {
            // 至少需要选择音频文件
            SaveBtn.IsEnabled = !string.IsNullOrWhiteSpace(AudioFileBox.Text);
        }

        private void BrowseAudioBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "音频文件|*.mp3;*.wav;*.flac;*.m4a;*.ogg|所有文件|*.*",
                Title = "选择音频文件"
            };

            if (dialog.ShowDialog() == true)
            {
                AudioFileBox.Text = dialog.FileName;
                UpdateSaveButtonState();
            }
        }

        private void TestAudioBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AudioFileBox.Text) || !File.Exists(AudioFileBox.Text))
            {
                MessageBox.Show("请先选择有效的音频文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 如果正在播放，则停止
                if (_previewPlayer?.PlaybackState == PlaybackState.Playing)
                {
                    StopPreview();
                    TestAudioBtn.Content = "▶️ 试听";
                    return;
                }

                // 开始播放试听
                StartPreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"试听失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StopPreview();
                TestAudioBtn.Content = "▶️ 试听";
            }
        }

        private void StartPreview()
        {
            StopPreview(); // 确保先停止之前的播放

            _previewReader = new AudioFileReader(AudioFileBox.Text);
            _previewPlayer = new WaveOutEvent();

            // 应用音量设置
            _previewReader.Volume = (float)VolumeSlider.Value;

            _previewPlayer.Init(_previewReader);
            
            // 播放完成事件
            _previewPlayer.PlaybackStopped += (sender, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    TestAudioBtn.Content = "▶️ 试听";
                    StopPreview();
                });
            };

            _previewPlayer.Play();
            TestAudioBtn.Content = "⏹️ 停止";
        }

        private void StopPreview()
        {
            _previewPlayer?.Stop();
            _previewPlayer?.Dispose();
            _previewPlayer = null;

            _previewReader?.Dispose();
            _previewReader = null;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证输入
                if (!ValidateInput()) return;

                // 更新映射
                _keyMapping.DisplayName = DisplayNameBox.Text?.Trim() ?? string.Empty;
                _keyMapping.ExactMatch = ExactMatchRadio.IsChecked == true;
                _keyMapping.Audio = CreateAudioSettings();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateInput()
        {
            // 验证音频文件
            if (string.IsNullOrWhiteSpace(AudioFileBox.Text))
            {
                MessageBox.Show("请选择音频文件", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!File.Exists(AudioFileBox.Text))
            {
                MessageBox.Show("音频文件不存在", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // 验证淡入淡出时间
            if (!int.TryParse(FadeInBox.Text, out var fadeIn) || fadeIn < 0)
            {
                MessageBox.Show("淡入时间必须是非负整数", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                FadeInBox.Focus();
                return false;
            }

            if (!int.TryParse(FadeOutBox.Text, out var fadeOut) || fadeOut < 0)
            {
                MessageBox.Show("淡出时间必须是非负整数", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                FadeOutBox.Focus();
                return false;
            }

            // 验证自定义时长
            if (CustomDurationRadio.IsChecked == true)
            {
                if (!int.TryParse(CustomDurationBox.Text, out var customDuration) || customDuration <= 0)
                {
                    MessageBox.Show("自定义时长必须是正整数", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CustomDurationBox.Focus();
                    return false;
                }
            }

            return true;
        }

        private AudioSettings CreateAudioSettings()
        {
            return new AudioSettings
            {
                FilePath = AudioFileBox.Text.Trim(),
                Volume = (float)VolumeSlider.Value,
                FadeInMs = int.Parse(FadeInBox.Text),
                FadeOutMs = int.Parse(FadeOutBox.Text),
                Interruptible = InterruptibleCheck.IsChecked ?? false,
                RepeatBehavior = RestartOnRepeatRadio.IsChecked == true ? RepeatBehavior.Restart : RepeatBehavior.Stop,
                DurationMode = CustomDurationRadio.IsChecked == true ? DurationMode.Custom : DurationMode.Default,
                CustomDurationSeconds = int.TryParse(CustomDurationBox.Text, out var duration) ? duration : 30
            };
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DurationRadio_Changed(object sender, RoutedEventArgs e)
        {
            UpdateCustomDurationPanelVisibility();
        }

        private void UpdateCustomDurationPanelVisibility()
        {
            if (CustomDurationPanel != null && CustomDurationRadio != null)
            {
                CustomDurationPanel.Visibility = CustomDurationRadio.IsChecked == true 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 窗口关闭时停止试听
            StopPreview();
            base.OnClosed(e);
        }
    }
}
