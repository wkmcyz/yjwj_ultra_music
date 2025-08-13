using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using UltAssist.Config;

namespace UltAssist
{
    public partial class KeyMappingEditDialog : Window
    {
        private KeyMapping _keyMapping;
        private readonly bool _isNewMapping;

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
            LoopCheck.IsChecked = _keyMapping.Audio.Loop;
            InterruptibleCheck.IsChecked = _keyMapping.Audio.Interruptible;

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
                // 创建临时的音频设置进行试听
                var tempAudio = CreateAudioSettings();
                
                // TODO: 实现音频试听功能
                // 这里可以创建一个临时的音频播放器来试听
                MessageBox.Show("试听功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"试听失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                Loop = LoopCheck.IsChecked ?? false,
                Interruptible = InterruptibleCheck.IsChecked ?? true
            };
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
