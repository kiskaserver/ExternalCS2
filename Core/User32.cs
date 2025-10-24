using System;
using System.Runtime.InteropServices;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Utils;
using Point = CS2GameHelper.Core.Data.Point;

namespace CS2GameHelper.Core;

public static class User32
{
    #region routines

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ClientToScreen(IntPtr hWnd, out Point lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hInstance);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr hInstance, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, Utility.Input[] pInputs, int cbSize);

    #endregion

    #region dpi awareness

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(nint dpiContext);

    public static bool TryEnablePerMonitorDpiAwareness()
    {
        try
        {
            if (SetProcessDpiAwarenessContext(DpiAwarenessContexts.PerMonitorAwareV2))
            {
                return true;
            }

            if (SetProcessDpiAwarenessContext(DpiAwarenessContexts.PerMonitorAware))
            {
                return true;
            }
        }
        catch (EntryPointNotFoundException)
        {
            // API not available; fall back to SetProcessDPIAware below.
        }

        try
        {
            return SetProcessDPIAware();
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static class DpiAwarenessContexts
    {
        public static readonly nint PerMonitorAwareV2 = new(-4);
        public static readonly nint PerMonitorAware = new(-3);
        public static readonly nint SystemAware = new(-2);
    }

    #endregion

    #region window styles

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ClipCursor(IntPtr lpRect);

    #endregion

    #region structures

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public Point Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    #endregion
}