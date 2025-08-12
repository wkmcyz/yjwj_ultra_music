using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Timer = System.Timers.Timer;
using TimerElapsedEventArgs = System.Timers.ElapsedEventArgs;
using UltAssist.Config;

namespace UltAssist.Input
{
    public class KeyListenerV2 : IDisposable
    {
        // Win32 API
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private LowLevelKeyboardProc _proc = null!;
        private IntPtr _hookID = IntPtr.Zero;
        
        // 当前按下的键状态
        private readonly HashSet<Keys> _pressedKeys = new();
        
        // 配置和状态
        private ListeningMode _listeningMode = ListeningMode.GameWindowOnly;
        private List<string> _gameProcessNames = new();
        private bool _enabled = true;
        
        // 游戏窗口检测定时器
        private readonly Timer _gameWindowTimer;
        private bool _isGameWindowActive = false;
        
        // 事件
        public event Action<KeyCombination>? KeyCombinationPressed;
        public event Action<bool>? GameWindowActiveChanged;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!_enabled)
                {
                    _pressedKeys.Clear(); // 清除按键状态
                }
            }
        }

        public bool IsGameWindowActive => _isGameWindowActive;

        public KeyListenerV2()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
            
            // 游戏窗口检测定时器 - 每500ms检测一次
            _gameWindowTimer = new Timer(500);
            _gameWindowTimer.Elapsed += CheckGameWindowStatus;
            _gameWindowTimer.Start();
        }

        public void UpdateSettings(ListeningMode mode, List<string> gameProcessNames)
        {
            _listeningMode = mode;
            _gameProcessNames = gameProcessNames?.ToList() ?? new();
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule?.ModuleName), 0);
        }

        private void CheckGameWindowStatus(object? sender, TimerElapsedEventArgs e)
        {
            try
            {
                bool wasActive = _isGameWindowActive;
                _isGameWindowActive = IsTargetGameWindowActive();
                
                if (wasActive != _isGameWindowActive)
                {
                    GameWindowActiveChanged?.Invoke(_isGameWindowActive);
                    
                    // 窗口切换时清除按键状态，避免跨窗口的按键残留
                    _pressedKeys.Clear();
                }
            }
            catch
            {
                // 忽略检测异常
            }
        }

        private bool IsTargetGameWindowActive()
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero) return false;

                GetWindowThreadProcessId(foregroundWindow, out uint processId);
                if (processId == 0) return false;

                using var process = Process.GetProcessById((int)processId);
                var processName = process.ProcessName + ".exe";
                
                return _gameProcessNames.Any(name => 
                    string.Equals(name, processName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _enabled)
            {
                bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                bool isKeyUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

                if (isKeyDown || isKeyUp)
                {
                    // 检查是否应该监听（根据监听模式）
                    bool shouldListen = _listeningMode == ListeningMode.Global || 
                                       (_listeningMode == ListeningMode.GameWindowOnly && _isGameWindowActive);
                    
                    if (shouldListen)
                    {
                        int vkCode = Marshal.ReadInt32(lParam);
                        var key = (Keys)vkCode;
                        
                        if (isKeyDown)
                        {
                            HandleKeyDown(key);
                        }
                        else if (isKeyUp)
                        {
                            HandleKeyUp(key);
                        }
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void HandleKeyDown(Keys key)
        {
            // 避免重复按下同一个键
            if (_pressedKeys.Contains(key)) return;
            
            _pressedKeys.Add(key);
            
            // 检查是否形成了有效的组合键
            CheckForKeyCombination();
        }

        private void HandleKeyUp(Keys key)
        {
            _pressedKeys.Remove(key);
        }

        private void CheckForKeyCombination()
        {
            if (_pressedKeys.Count == 0) return;
            
            // 转换为标准化的按键名称
            var keyNames = _pressedKeys.Select(NormalizeKeyName).Where(k => !string.IsNullOrEmpty(k)).ToList();
            if (keyNames.Count == 0) return;
            
            var combination = new KeyCombination { Keys = keyNames };
            
            // 触发按键组合事件
            KeyCombinationPressed?.Invoke(combination);
        }

        private static string NormalizeKeyName(Keys key)
        {
            // 将 Windows Forms Keys 转换为标准化的按键名称
            return key switch
            {
                // 修饰键
                Keys.Control or Keys.LControlKey or Keys.RControlKey => "Ctrl",
                Keys.Shift or Keys.LShiftKey or Keys.RShiftKey => "Shift", 
                Keys.Alt or Keys.LMenu or Keys.RMenu => "Alt",
                Keys.LWin or Keys.RWin => "Win",
                
                // 鼠标按键（需要额外的鼠标钩子才能捕获，这里先预留）
                // 实际鼠标事件需要单独的鼠标钩子
                
                // 特殊键
                Keys.Space => "Space",
                Keys.Enter => "Enter",
                Keys.Back => "Backspace",
                Keys.Tab => "Tab",
                Keys.Escape => "Escape",
                Keys.Delete => "Delete",
                Keys.Insert => "Insert",
                Keys.Home => "Home",
                Keys.End => "End",
                Keys.PageUp => "PageUp",
                Keys.PageDown => "PageDown",
                
                // 方向键
                Keys.Up => "Up",
                Keys.Down => "Down",
                Keys.Left => "Left",
                Keys.Right => "Right",
                
                // 功能键
                Keys.F1 => "F1", Keys.F2 => "F2", Keys.F3 => "F3", Keys.F4 => "F4",
                Keys.F5 => "F5", Keys.F6 => "F6", Keys.F7 => "F7", Keys.F8 => "F8",
                Keys.F9 => "F9", Keys.F10 => "F10", Keys.F11 => "F11", Keys.F12 => "F12",
                
                // 数字键
                Keys.D0 => "0", Keys.D1 => "1", Keys.D2 => "2", Keys.D3 => "3", Keys.D4 => "4",
                Keys.D5 => "5", Keys.D6 => "6", Keys.D7 => "7", Keys.D8 => "8", Keys.D9 => "9",
                
                // 字母键
                >= Keys.A and <= Keys.Z => key.ToString(),
                
                // 其他常用键
                Keys.OemMinus => "-",
                Keys.Oemplus => "+",
                Keys.OemOpenBrackets => "[",
                Keys.OemCloseBrackets => "]",
                Keys.OemSemicolon => ";",
                Keys.OemQuotes => "'",
                Keys.Oemcomma => ",",
                Keys.OemPeriod => ".",
                Keys.OemQuestion => "/",
                Keys.Oemtilde => "`",
                Keys.OemBackslash => "\\",
                
                // 小键盘
                Keys.NumPad0 => "Num0", Keys.NumPad1 => "Num1", Keys.NumPad2 => "Num2",
                Keys.NumPad3 => "Num3", Keys.NumPad4 => "Num4", Keys.NumPad5 => "Num5",
                Keys.NumPad6 => "Num6", Keys.NumPad7 => "Num7", Keys.NumPad8 => "Num8",
                Keys.NumPad9 => "Num9",
                Keys.Add => "NumAdd", Keys.Subtract => "NumSub", 
                Keys.Multiply => "NumMul", Keys.Divide => "NumDiv",
                Keys.Decimal => "NumDot",
                
                _ => string.Empty // 忽略不支持的键
            };
        }

        public void Dispose()
        {
            _gameWindowTimer?.Stop();
            _gameWindowTimer?.Dispose();
            
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }
    }
}
