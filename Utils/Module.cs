using System.Diagnostics;

namespace CS2GameHelper.Utils;

public sealed class Module : IDisposable
{
    public Module(Process process, ProcessModule processModule)
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
        ProcessModule = processModule ?? throw new ArgumentNullException(nameof(processModule));
        BaseAddress = ProcessModule.BaseAddress;
        Size = ProcessModule.ModuleMemorySize;
    }

    public Module(Process process, IntPtr baseAddress, int size)
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
        BaseAddress = baseAddress;
        Size = size;
    }

    public Process? Process { get; private set; }

    public ProcessModule? ProcessModule { get; private set; }

    public IntPtr BaseAddress { get; }

    public int Size { get; }

    // Convenience instance wrapper so callers can use module.Read<T>(offset)
    // without depending on the extension method resolution. This forwards
    // to the Utility.Read extension implementation.
    public T Read<T>(int offset)
        where T : unmanaged
    {
        return Utility.Read<T>(this, offset);
    }

    public void Dispose()
    {
        Process?.Dispose();
        Process = null;

        ProcessModule?.Dispose();
        ProcessModule = null;
    }
}