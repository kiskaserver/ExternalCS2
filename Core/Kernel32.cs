using System.Runtime.InteropServices;

namespace CS2GameHelper.Core;

public abstract class Kernel32
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
        [Out] IntPtr lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct MODULEENTRY32
{
    public uint dwSize;
    public uint th32ModuleID;
    public uint th32ProcessID;
    public uint GlblcntUsage;
    public uint ProccntUsage;
    public IntPtr modBaseAddr;
    public uint modBaseSize;
    public IntPtr hModule;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string szModule;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string szExePath;
}