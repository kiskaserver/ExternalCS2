using System.Diagnostics;
using CS2GameHelper.Core;
using User32 = CS2GameHelper.Core.User32;

namespace CS2GameHelper.Utils;

public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

public enum HookType
{
    WH_KEYBOARD_LL = 13,
    WH_MOUSE_LL = 14
}

public class GlobalHook : IDisposable
{
    public GlobalHook(HookType hookType, HookProc hookProc)
    {
        HookType = hookType;
        _hookProc = hookProc;
        HookHandle = Hook(HookType, hookProc);
    }

    private HookType HookType { get; }
    private HookProc? _hookProc;
    public IntPtr HookHandle { get; private set; }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~GlobalHook()
    {
        ReleaseUnmanagedResources();
    }

    private void ReleaseUnmanagedResources()
    {
        if (HookHandle != IntPtr.Zero)
        {
            UnHook(HookHandle);
            HookHandle = IntPtr.Zero;
        }

        _hookProc = null;
    }

    private static IntPtr Hook(HookType hookType, HookProc hookProc)
    {
        using var currentProcess = Process.GetCurrentProcess();
        using var curModule = currentProcess.MainModule;
        if (curModule is null) throw new ArgumentNullException(nameof(curModule));

        var hHook = User32.SetWindowsHookEx((int)hookType, hookProc,
            Kernel32.GetModuleHandle(curModule.ModuleName), 0);
        if (hHook == IntPtr.Zero) throw new ArgumentException("Hook failed.");

        return hHook;
    }

    private static void UnHook(IntPtr hHook)
    {
        if (!User32.UnhookWindowsHookEx(hHook)) throw new ArgumentException("UnHook failed.");
    }
}