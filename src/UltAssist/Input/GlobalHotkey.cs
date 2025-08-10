using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace UltAssist.Input
{
    public sealed class GlobalHotkey : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Low-level keyboard hook (fallback when RegisterHotKey fails)
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private readonly HwndSource source;
        private readonly int id;
        private readonly uint vk;
        private readonly uint modifiers;
        private bool registered;
        private IntPtr hookId = IntPtr.Zero;
        private readonly LowLevelKeyboardProc hookProc;
        private DateTime lastInvoke = DateTime.MinValue;
        public event Action? Pressed;

        public GlobalHotkey(IntPtr handle, uint vk, uint modifiers = 0, int id = 1)
        {
            this.id = id;
            this.vk = vk;
            this.modifiers = modifiers;
            source = HwndSource.FromHwnd(handle);
            source.AddHook(HwndHook);

            // Only register system hotkey when modifiers are present to避免抢占裸键 V
            registered = modifiers != 0 && RegisterHotKey(handle, this.id, modifiers, vk);

            // Always install low-level keyboard hook to capture in games; we'll de-duplicate
            hookProc = HookCallback;
            using var curProc = Process.GetCurrentProcess();
            using var curModule = curProc.MainModule!;
            IntPtr hModule = GetModuleHandle(curModule.ModuleName);
            hookId = SetWindowsHookEx(13 /*WH_KEYBOARD_LL*/, hookProc, hModule, 0);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == id)
            {
                Pressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            try { if (registered) UnregisterHotKey(source.Handle, id); } catch { }
            if (hookId != IntPtr.Zero)
            {
                try { UnhookWindowsHookEx(hookId); } catch { }
                hookId = IntPtr.Zero;
            }
            source.RemoveHook(HwndHook);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // wParam: WM_KEYDOWN=0x0100, WM_SYSKEYDOWN=0x0104
            const int WM_KEYDOWN = 0x0100;
            const int WM_SYSKEYDOWN = 0x0104;
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                // KBDLLHOOKSTRUCT layout: vkCode at offset 0
                int vkCode = Marshal.ReadInt32(lParam);
                if ((uint)vkCode == vk && modifiers == 0)
                {
                    TryInvokeOnce();
                }
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private void TryInvokeOnce()
        {
            var now = DateTime.UtcNow;
            if ((now - lastInvoke).TotalMilliseconds < 150) return;
            lastInvoke = now;
            Pressed?.Invoke();
        }
    }
}

