using System.Diagnostics;
using CS2GameHelper.Core;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Data.Game;

public class GameProcess : ThreadedServiceBase
{
    #region constants

    private const string NameProcess = "cs2";

    private const string NameModule = "client.dll";
    private const string NameEngineModule = "engine2.dll";

    private const string NameWindow = "Counter-Strike 2";

    #endregion

    #region properties

    protected override string ThreadName => nameof(GameProcess);

    protected override TimeSpan ThreadFrameSleep { get; set; } = new(0, 0, 0, 0, 500);

    public System.Diagnostics.Process? Process { get; private set; }

    public Module? ModuleClient { get; private set; }
    public Module? ModuleEngine { get; private set; }

    private IntPtr WindowHwnd { get; set; }
    
    private IntPtr _hijackedHandle = IntPtr.Zero;

    public Rectangle WindowRectangleClient { get; private set; }

    private bool WindowActive { get; set; }

    private bool WindowDetected => WindowHwnd != IntPtr.Zero && WindowRectangleClient.Width > 0 && WindowRectangleClient.Height > 0;

    public bool HasWindow => WindowDetected;

    public bool IsValid => Process is { HasExited: false } && ModuleClient != null && WindowDetected;

    // True when the game's window exists and is the foreground window
    public bool IsWindowActive => WindowHwnd != IntPtr.Zero && WindowHwnd == User32.GetForegroundWindow();

    #endregion

    #region routines

    public override void Dispose()
    {
        InvalidateWindow();
        InvalidateModules();
        DiskHelper.Shutdown();
        base.Dispose();
    }


    protected override async void FrameAction()
    {
        if (!EnsureProcessAndModules())
        {
            InvalidateModules();
        }

        if (!EnsureWindow())
        {
            InvalidateWindow();
        }

        await Task.Delay(ThreadFrameSleep);
    }


    private void InvalidateModules()
    {
        ModuleClient?.Dispose();
        ModuleEngine?.Dispose();
        ModuleClient = default;
        ModuleEngine = default;

        if (_hijackedHandle != IntPtr.Zero)
        {
            Kernel32.CloseHandle(_hijackedHandle);
            _hijackedHandle = IntPtr.Zero;
        }

        // Clear the driver target so subsequent IOCTLs cannot be misrouted
        // at a PID that has since been reused by another, unrelated process.
        DiskHelper.TargetPid = 0;

        Process?.Dispose();
        Process = default;
    }

    private void InvalidateWindow()
    {
        WindowHwnd = IntPtr.Zero;
        WindowRectangleClient = Rectangle.Empty;
        WindowActive = false;
    }

    private bool EnsureProcessAndModules()
    {
        if (Process == null)
        {
            // GetProcessesByName returns ALL matching processes; we keep the first
            // and must dispose the rest, otherwise their Win32 handles leak.
            var processes = System.Diagnostics.Process.GetProcessesByName(NameProcess);
            Process = processes.FirstOrDefault();
            foreach (var p in processes)
            {
                if (!ReferenceEquals(p, Process)) p.Dispose();
            }
        }
        if (Process == null || Process.HasExited)
        {
            return false;
        }
        
        if (_hijackedHandle == IntPtr.Zero)
        {
            _hijackedHandle = Kernel32.OpenProcess(0x0010 | 0x0020 | 0x0400, false, Process.Id); // PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_QUERY_INFORMATION
        }

        // Hand the PID to the kernel driver wrapper. Initialize() is a no-op
        // when the device is already open; it returns false (silently) when
        // DiskHelper.sys is not loaded, in which case Utility falls back to
        // Kernel32.ReadProcessMemory.
        DiskHelper.Initialize();
        DiskHelper.TargetPid = (ulong)Process.Id;

        ModuleClient ??= Process.GetModule(NameModule);
        if (ModuleClient != null) ModuleClient.GameProcess = this;
        
        ModuleEngine ??= Process.GetModule(NameEngineModule);
        if (ModuleEngine != null) ModuleEngine.GameProcess = this;

        return ModuleClient != null;
    }
    
    public IntPtr GetProcessHandle()
    {
        return _hijackedHandle != IntPtr.Zero ? _hijackedHandle : (Process?.Handle ?? IntPtr.Zero);
    }


    private bool EnsureWindow()
    {
        WindowHwnd = User32.FindWindow(null!, NameWindow);
        if (WindowHwnd == IntPtr.Zero)
        {
            return false;
        }

        WindowRectangleClient = Utility.GetClientRectangle(WindowHwnd);
        if (WindowRectangleClient.Width <= 0 || WindowRectangleClient.Height <= 0)
        {
            return false;
        }

        WindowActive = WindowHwnd == User32.GetForegroundWindow();

        return true;
    }

    #endregion
}