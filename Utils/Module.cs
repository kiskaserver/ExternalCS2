using System.Diagnostics;

namespace CS2GameHelper.Utils;

public sealed class Module : IDisposable
{
    public CS2GameHelper.Data.Game.GameProcess? GameProcess { get; set; }

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
        // Module does NOT own Process — it is provided by GameProcess and disposed there.
        // ProcessModule instances come from Process.Modules and are tied to the Process
        // lifetime; disposing them here is also not our responsibility. We only drop
        // references so the GC can reclaim wrappers.
        Process = null;
        ProcessModule = null;
    }
}