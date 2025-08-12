using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using UltAssist.Config;

namespace UltAssist.Input
{
    public class SimpleKeyRecorder : IDisposable
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

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private LowLevelKeyboardProc _proc = null!;
        private IntPtr _hookID = IntPtr.Zero;
        private bool _isRecording = false;
        
        // 当前按下的键
        private readonly HashSet<Keys> _pressedKeys = new();
        
        public event Action<KeyCombination>? KeyCombinationRecorded;

        public SimpleKeyRecorder()
        {
            _proc = HookCallback;
        }

        public void StartRecording()
        {
            if (_isRecording) return;
            
            _pressedKeys.Clear();
            _isRecording = true;
            _hookID = SetHook(_proc);
        }

        public void StopRecording()
        {
            if (!_isRecording) return;
            
            _isRecording = false;
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            _pressedKeys.Clear();
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule?.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isRecording)
            {
                bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                bool isKeyUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

                int vkCode = Marshal.ReadInt32(lParam);
                var key = (Keys)vkCode;

                if (isKeyDown)
                {
                    // 添加按下的键
                    if (!_pressedKeys.Contains(key))
                    {
                        _pressedKeys.Add(key);
                        
                        // 检查并报告当前组合键
                        CheckAndReportCombination();
                    }
                }
                else if (isKeyUp)
                {
                    // 移除释放的键
                    _pressedKeys.Remove(key);
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void CheckAndReportCombination()
        {
            if (_pressedKeys.Count == 0) return;
            
            // 转换为标准化的按键名称
            var keyNames = _pressedKeys.Select(NormalizeKeyName).Where(k => !string.IsNullOrEmpty(k)).ToList();
            if (keyNames.Count == 0) return;
            
            // 排序以确保一致性（修饰键在前）
            var sortedKeys = keyNames.OrderBy(k => GetKeyPriority(k)).ToList();
            
            var combination = new KeyCombination { Keys = sortedKeys };
            
            // 报告按键组合
            KeyCombinationRecorded?.Invoke(combination);
        }
        
        private static int GetKeyPriority(string keyName)
        {
            return keyName switch
            {
                "Ctrl" => 1,
                "Shift" => 2,
                "Alt" => 3,
                "Win" => 4,
                _ => 5
            };
        }

        private static string NormalizeKeyName(Keys key)
        {
            return key switch
            {
                // 修饰键
                Keys.Control or Keys.LControlKey or Keys.RControlKey => "Ctrl",
                Keys.Shift or Keys.LShiftKey or Keys.RShiftKey => "Shift",
                Keys.Alt or Keys.LMenu or Keys.RMenu => "Alt",
                Keys.LWin or Keys.RWin => "Win",
                
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
            StopRecording();
        }
    }
}
