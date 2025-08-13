using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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
        }

        public void SetTargetProcessName(string processName)
        {
            _targetProcessName = processName;
        }

        public void UpdateListeningStatus(bool isEnabled)
        {
            Dispatcher.Invoke(() =>
            {
                if (isEnabled)
                {
                    ListeningIndicator.Fill = new SolidColorBrush(Color.FromRgb(163, 190, 140)); // 绿色
                    ListeningStatusText.Text = "监听: 已启用";
                }
                else
                {
                    ListeningIndicator.Fill = new SolidColorBrush(Color.FromRgb(191, 97, 106)); // 红色
                    ListeningStatusText.Text = "监听: 已禁用";
                }
            });
        }

        public void UpdateGameStatus(bool isGameActive)
        {
            Dispatcher.Invoke(() =>
            {
                if (isGameActive)
                {
                    GameIndicator.Fill = new SolidColorBrush(Color.FromRgb(163, 190, 140)); // 绿色
                    GameStatusText.Text = "游戏: 已检测到";
                }
                else
                {
                    GameIndicator.Fill = new SolidColorBrush(Color.FromRgb(191, 97, 106)); // 红色
                    GameStatusText.Text = "游戏: 未检测到";
                }
            });
        }

        public void UpdateLastKey(string keyName, DateTime timestamp)
        {
            Dispatcher.Invoke(() =>
            {
                var timeStr = timestamp.ToString("HH:mm:ss");
                LastKeyText.Text = $"最后按键: {keyName} ({timeStr})";
            });
        }

        public void UpdatePlayingAudios(string[] audioFiles)
        {
            Dispatcher.Invoke(() =>
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
