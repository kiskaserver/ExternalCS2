using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using CS2GameHelper.Core;
using CS2GameHelper.Core.Data;
using Point = System.Drawing.Point;

namespace CS2GameHelper.Utils
{
    public enum MouseMessages
    {
        WmMouseMove = 0x0200,
        WmLButtonDown = 0x0201,
        WmLButtonUp = 0x0202,
        WmRButtonDown = 0x0204,
        WmRButtonUp = 0x0205
    }

    public enum KeyboardMessages
    {
        WmKeyDown = 0x0100,
        WmKeyUp = 0x0101,
        WmSysKeyDown = 0x0104,
        WmSysKeyUp = 0x0105
    }

    public class UserInputHandler : IDisposable
    {
        public Point LastMouseDelta { get; private set; }
        public DateTime LastMouseMoveTime { get; private set; }
        public bool IsLeftMouseDown { get; private set; }
        public bool IsRightMouseDown { get; private set; }

        private readonly HashSet<Keys> _pressedKeys = new();
        private GlobalHook? _mouseHook;
        private GlobalHook? _keyboardHook;
        private int _lastMouseX, _lastMouseY;

        public UserInputHandler()
        {
            _mouseHook = new GlobalHook(HookType.WH_MOUSE_LL, MouseHookCallback);
            _keyboardHook = new GlobalHook(HookType.WH_KEYBOARD_LL, KeyboardHookCallback);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var msg = (MouseMessages)wParam;
                var mouseStruct = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);

                switch (msg)
                {
                    case MouseMessages.WmMouseMove:
                        var dx = mouseStruct.Point.X - _lastMouseX;
                        var dy = mouseStruct.Point.Y - _lastMouseY;
                        LastMouseDelta = new Point(dx, dy);
                        LastMouseMoveTime = DateTime.Now;
                        _lastMouseX = mouseStruct.Point.X;
                        _lastMouseY = mouseStruct.Point.Y;
                        break;

                    case MouseMessages.WmLButtonDown:
                        IsLeftMouseDown = true;
                        break;
                    case MouseMessages.WmLButtonUp:
                        IsLeftMouseDown = false;
                        break;
                    case MouseMessages.WmRButtonDown:
                        IsRightMouseDown = true;
                        break;
                    case MouseMessages.WmRButtonUp:
                        IsRightMouseDown = false;
                        break;
                }
            }

            return User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // lParam указывает на KBDLLHOOKSTRUCT, но нам нужен только vkCode (первые 4 байта)
                var vkCode = Marshal.ReadInt32(lParam);
                var key = (Keys)vkCode;
                var msg = (KeyboardMessages)wParam;

                if (msg == KeyboardMessages.WmKeyDown || msg == KeyboardMessages.WmSysKeyDown)
                {
                    _pressedKeys.Add(key);
                }
                else if (msg == KeyboardMessages.WmKeyUp || msg == KeyboardMessages.WmSysKeyUp)
                {
                    _pressedKeys.Remove(key);
                }
            }

            return User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        // 🔑 ЭТОТ МЕТОД БЫЛ ГЛАВНЫМ — ТЕПЕРЬ ОН РАБОТАЕТ!
        public bool IsKeyDown(Keys key)
        {
            return _pressedKeys.Contains(key);
        }

        public void Dispose()
        {
            _mouseHook?.Dispose();
            _keyboardHook?.Dispose();
            _pressedKeys.Clear();
        }
    }
}