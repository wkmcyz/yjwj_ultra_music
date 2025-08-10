using System;
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

        private readonly HwndSource source;
        private readonly int id;
        public event Action? Pressed;

        public GlobalHotkey(IntPtr handle, uint vk, uint modifiers = 0, int id = 1)
        {
            this.id = id;
            source = HwndSource.FromHwnd(handle);
            source.AddHook(HwndHook);
            RegisterHotKey(handle, this.id, modifiers, vk);
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
            try { UnregisterHotKey(source.Handle, id); } catch { }
            source.RemoveHook(HwndHook);
        }
    }
}

