using System;
using System.Drawing;
using System.Runtime.InteropServices;
using CS2GameHelper.Core;

namespace CS2GameHelper.Utils
{
    public enum MouseMessages { WmLButtonDown = 0x0201, WmLButtonUp = 0x0202, WmMouseMove = 0x0200 }

    public class UserInputHandler : IDisposable
    {
        public Point LastMouseDelta { get; private set; }
        public DateTime LastMouseMoveTime { get; private set; }

        private GlobalHook? _mouseHook;
        private int _lastMouseX, _lastMouseY;

        public UserInputHandler()
        {
            _mouseHook = new GlobalHook(HookType.WH_MOUSE_LL, MouseHookCallback);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (MouseMessages)wParam == MouseMessages.WmMouseMove)
            {
                var mouseInput = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);
                var dx = mouseInput.Point.X - _lastMouseX;
                var dy = mouseInput.Point.Y - _lastMouseY;
                LastMouseDelta = new Point(dx, dy);
                LastMouseMoveTime = DateTime.Now;
                _lastMouseX = mouseInput.Point.X;
                _lastMouseY = mouseInput.Point.Y;
            }
            return User32.CallNextHookEx(_mouseHook?.HookHandle ?? IntPtr.Zero, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            _mouseHook?.Dispose();
        }
    }
}