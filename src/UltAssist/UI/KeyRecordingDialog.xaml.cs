using System;
using System.Windows;
using System.Windows.Input;
using UltAssist.Config;
using UltAssist.Input;

namespace UltAssist
{
    public partial class KeyRecordingDialog : Window
    {
        private readonly SimpleKeyRecorder _keyRecorder;
        private KeyCombination? _recordedKey;
        private bool _isRecording = true;

        public KeyCombination? RecordedKey => _recordedKey;

        public KeyRecordingDialog()
        {
            InitializeComponent();
            
            // 使用简化的按键录制器
            _keyRecorder = new SimpleKeyRecorder();
            _keyRecorder.KeyCombinationRecorded += OnKeyCombinationRecorded;
            
            Loaded += KeyRecordingDialog_Loaded;
            Closed += KeyRecordingDialog_Closed;
        }

        private void KeyRecordingDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // 聚焦到窗口并开始录制
            Focus();
            Activate();
            _keyRecorder.StartRecording();
        }

        private void OnKeyCombinationRecorded(KeyCombination combination)
        {
            if (!_isRecording) return;

            Dispatcher.Invoke(() =>
            {
                // 检查是否是ESC键（取消录制）
                if (combination.Keys.Count == 1 && combination.Keys.Contains("Escape"))
                {
                    CancelRecording();
                    return;
                }

                // 检查是否是被禁止的按键组合
                if (InputManagerV2.IsProhibitedKeyCombination(combination))
                {
                    StatusText.Text = $"按键组合 {combination.ToDisplayString()} 是保留按键，请选择其他按键";
                    StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(191, 97, 106));
                    return;
                }

                // 记录按键
                _recordedKey = combination;
                RecordedKeyText.Text = combination.ToDisplayString();
                RecordedKeyBorder.Visibility = Visibility.Visible;
                
                InstructionText.Text = "检测到按键组合：";
                StatusText.Text = "点击\"确认\"保存，或按其他按键重新录制";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(94, 129, 172));
                
                ConfirmBtn.IsEnabled = true;
            });
        }

        private void CancelRecording()
        {
            _isRecording = false;
            _recordedKey = null;
            _keyRecorder.StopRecording();
            DialogResult = false;
            Close();
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_recordedKey != null)
            {
                _isRecording = false;
                _keyRecorder.StopRecording();
                DialogResult = true;
                Close();
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            CancelRecording();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // 阻止窗口默认的键盘处理，让全局钩子处理
            e.Handled = true;
        }

        private void KeyRecordingDialog_Closed(object? sender, EventArgs e)
        {
            _keyRecorder?.Dispose();
        }
    }
}
