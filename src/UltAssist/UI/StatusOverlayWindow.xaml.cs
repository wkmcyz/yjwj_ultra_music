using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UltAssist.Config;

namespace UltAssist.UI
{
    public partial class StatusOverlayWindow : Window
    {
        // Win32 API for getting screen information
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private readonly DispatcherTimer _positionUpdateTimer;
        private string _targetProcessName = "NarakaBladepoint.exe";
        private OverlayStyle _currentStyle = OverlayStyle.None;

        public event Action? MinimizeMainWindow;
        public event Action? CloseApplication;

        public StatusOverlayWindow()
        {
            InitializeComponent();
            
            // 初始化位置更新定时器
            _positionUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000) // 每秒检查一次位置
            };
            _positionUpdateTimer.Tick += UpdatePosition;
            _positionUpdateTimer.Start();

            // 允许拖拽窗口
            MouseLeftButtonDown += (s, e) => DragMove();
            
            // 初始位置
            UpdatePosition(null, null);
            
            // 初始状态为隐藏（等待设置显示样式）
            Hide();
        }

        public void SetTargetProcessName(string processName)
        {
            _targetProcessName = processName;
        }

        public void SetDisplayStyle(OverlayStyle style)
        {
            _currentStyle = style;
            System.Diagnostics.Debug.WriteLine($"StatusOverlay: Setting display style to {style}");
            UpdateDisplayMode();
        }

        private void UpdateDisplayMode()
        {
            Dispatcher.Invoke(() =>
            {
                // 隐藏所有模式
                SimpleMode.Visibility = Visibility.Collapsed;
                CompleteMode.Visibility = Visibility.Collapsed;

                switch (_currentStyle)
                {
                    case OverlayStyle.None:
                        Hide();
                        break;

                    case OverlayStyle.StatusOnly:
                        SimpleMode.Visibility = Visibility.Visible;
                        Width = 200;
                        Height = 50;
                        Show();
                        break;

                    case OverlayStyle.DebugPanel:
                        CompleteMode.Visibility = Visibility.Visible;
                        Width = 400;
                        Height = 80;
                        Show();
                        break;
                }
            });
        }

        public void UpdateListeningStatus(bool isEnabled)
        {
            Dispatcher.Invoke(() =>
            {
                var greenBrush = new SolidColorBrush(Color.FromRgb(163, 190, 140)); // 绿色
                var redBrush = new SolidColorBrush(Color.FromRgb(191, 97, 106)); // 红色
                var statusText = isEnabled ? "监听: 已启用" : "监听: 已禁用";

                // 更新简单模式
                if (SimpleListeningIndicator != null)
                {
                    SimpleListeningIndicator.Fill = isEnabled ? greenBrush : redBrush;
                }
                if (SimpleListeningText != null)
                {
                    SimpleListeningText.Text = statusText;
                }

                // 更新完整模式
                if (ListeningIndicator != null)
                {
                    ListeningIndicator.Fill = isEnabled ? greenBrush : redBrush;
                }
                if (ListeningStatusText != null)
                {
                    ListeningStatusText.Text = statusText;
                }
            });
        }

        public void UpdateGameStatus(bool isGameActive)
        {
            Dispatcher.Invoke(() =>
            {
                // 只在完整模式下显示游戏状态
                if (_currentStyle == OverlayStyle.DebugPanel)
                {
                    var greenBrush = new SolidColorBrush(Color.FromRgb(163, 190, 140)); // 绿色
                    var redBrush = new SolidColorBrush(Color.FromRgb(191, 97, 106)); // 红色

                    if (GameIndicator != null)
                    {
                        GameIndicator.Fill = isGameActive ? greenBrush : redBrush;
                    }
                    if (GameStatusText != null)
                    {
                        GameStatusText.Text = isGameActive ? "游戏: 已检测到" : "游戏: 未检测到";
                    }
                }
            });
        }

        public void UpdateLastKey(string keyName, DateTime timestamp)
        {
            Dispatcher.Invoke(() =>
            {
                // 只在完整模式下显示最后按键
                if (_currentStyle == OverlayStyle.DebugPanel && LastKeyText != null)
                {
                    var timeStr = timestamp.ToString("HH:mm:ss");
                    LastKeyText.Text = $"最后按键: {keyName} ({timeStr})";
                }
            });
        }

        public void UpdatePlayingAudios(string[] audioFiles)
        {
            Dispatcher.Invoke(() =>
            {
                // 只在完整模式下显示播放信息
                if (_currentStyle == OverlayStyle.DebugPanel && PlayingAudiosText != null)
                {
                    if (audioFiles.Length == 0)
                    {
                        PlayingAudiosText.Text = "播放: -";
                    }
                    else if (audioFiles.Length == 1)
                    {
                        var fileName = System.IO.Path.GetFileNameWithoutExtension(audioFiles[0]);
                        PlayingAudiosText.Text = $"播放: {fileName}";
                    }
                    else
                    {
                        PlayingAudiosText.Text = $"播放: {audioFiles.Length}个音频";
                    }
                }
            });
        }

        private void UpdatePosition(object? sender, EventArgs? e)
        {
            try
            {
                // 获取目标进程窗口
                IntPtr targetWindow = FindTargetGameWindow();
                
                if (targetWindow != IntPtr.Zero)
                {
                    // 获取游戏窗口所在的显示器
                    IntPtr monitor = MonitorFromWindow(targetWindow, MONITOR_DEFAULTTONEAREST);
                    
                    MONITORINFO monitorInfo = new MONITORINFO();
                    monitorInfo.cbSize = (uint)Marshal.SizeOf(monitorInfo);
                    
                    if (GetMonitorInfo(monitor, ref monitorInfo))
                    {
                        // 将状态栏放在该显示器的顶部中央
                        var monitorWidth = monitorInfo.rcWork.Right - monitorInfo.rcWork.Left;
                        var overlayWidth = (int)Width;
                        
                        var newLeft = monitorInfo.rcWork.Left + (monitorWidth - overlayWidth) / 2;
                        var newTop = monitorInfo.rcWork.Top + 10; // 距离顶部10像素
                        
                        Left = newLeft;
                        Top = newTop;
                    }
                }
                else
                {
                    // 没有找到游戏窗口，放在主显示器顶部
                    var primaryScreen = Screen.PrimaryScreen;
                    if (primaryScreen != null)
                    {
                        var screenWidth = primaryScreen.WorkingArea.Width;
                        var overlayWidth = (int)Width;
                        
                        Left = (screenWidth - overlayWidth) / 2;
                        Top = 10;
                    }
                }
            }
            catch
            {
                // 发生错误时使用默认位置
            }
        }

        private IntPtr FindTargetGameWindow()
        {
            try
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero) return IntPtr.Zero;

                GetWindowThreadProcessId(foregroundWindow, out int processId);
                
                var process = System.Diagnostics.Process.GetProcessById(processId);
                if (process?.ProcessName + ".exe" == _targetProcessName)
                {
                    return foregroundWindow;
                }
            }
            catch
            {
                // 忽略错误
            }
            
            return IntPtr.Zero;
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            MinimizeMainWindow?.Invoke();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            CloseApplication?.Invoke();
        }

        protected override void OnClosed(EventArgs e)
        {
            _positionUpdateTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
