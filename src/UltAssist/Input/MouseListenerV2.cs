using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UltAssist.Input
{
    public class MouseListenerV2 : IDisposable
    {
        // Win32 API
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;

        private LowLevelMouseProc _proc = null!;
        private IntPtr _hookID = IntPtr.Zero;
        private bool _enabled = true;

        public event Action<string>? MouseButtonPressed; // 鼠标按键按下事件
        public event Action<string>? MouseButtonReleased; // 鼠标按键释放事件

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public MouseListenerV2()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return SetWindowsHookEx(WH_MOUSE_LL, proc,
                GetModuleHandle(curModule?.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _enabled)
            {
                string? buttonName = null;
                bool isPressed = false;

                switch (wParam.ToInt32())
                {
                    case WM_LBUTTONDOWN:
                        buttonName = "LeftMouse";
                        isPressed = true;
                        break;
                    case WM_LBUTTONUP:
                        buttonName = "LeftMouse";
                        isPressed = false;
                        break;
                    case WM_RBUTTONDOWN:
                        buttonName = "RightMouse";
                        isPressed = true;
                        break;
                    case WM_RBUTTONUP:
                        buttonName = "RightMouse";
                        isPressed = false;
                        break;
                    case WM_MBUTTONDOWN:
                        buttonName = "MiddleMouse";
                        isPressed = true;
                        break;
                    case WM_MBUTTONUP:
                        buttonName = "MiddleMouse";
                        isPressed = false;
                        break;
                    case WM_XBUTTONDOWN:
                        // 侧键需要额外解析
                        var xButtonInfo = Marshal.ReadInt32(lParam + 10); // HIWORD of mouseData
                        buttonName = (xButtonInfo & 0x0001) != 0 ? "Mouse4" : "Mouse5";
                        isPressed = true;
                        break;
                    case WM_XBUTTONUP:
                        var xButtonInfo2 = Marshal.ReadInt32(lParam + 10);
                        buttonName = (xButtonInfo2 & 0x0001) != 0 ? "Mouse4" : "Mouse5";
                        isPressed = false;
                        break;
                }

                if (!string.IsNullOrEmpty(buttonName))
                {
                    if (isPressed)
                    {
                        MouseButtonPressed?.Invoke(buttonName);
                    }
                    else
                    {
                        MouseButtonReleased?.Invoke(buttonName);
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }
    }
}
