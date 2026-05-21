using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CS2GameHelper.Core;

/// <summary>
/// Managed wrapper around the <c>DiskHelper.sys</c> kernel-mode driver
/// (see <c>MemoryTool/Driver/Driver.c</c>). All memory I/O is routed
/// through <c>DeviceIoControl</c> + the driver's <c>MmCopyVirtualMemory</c>
/// implementation, bypassing user-mode access checks
/// (<c>PROCESS_VM_READ</c> / <c>PROCESS_VM_WRITE</c>).
///
/// Layout of <see cref="MEMORY_OPERATION"/> and the IOCTL constants
/// MUST stay in lock-step with <c>MemoryTool/Shared/MemoryTool.h</c>.
/// </summary>
public static class DiskHelper
{
    // -----------------------------------------------------------------
    // Native constants — must match Shared/MemoryTool.h
    // -----------------------------------------------------------------
    private const string UsermodeDevicePath = @"\\.\DiskHelper";

    private const uint FILE_DEVICE_DISK = 0x00000007;
    private const uint METHOD_BUFFERED  = 0;
    private const uint FILE_ANY_ACCESS  = 0;

    private const uint GENERIC_READ  = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    public static readonly uint IOCTL_DISK_READ_MEMORY =
        CtlCode(FILE_DEVICE_DISK, 0x800, METHOD_BUFFERED, FILE_ANY_ACCESS);

    public static readonly uint IOCTL_DISK_WRITE_MEMORY =
        CtlCode(FILE_DEVICE_DISK, 0x801, METHOD_BUFFERED, FILE_ANY_ACCESS);

    private const int MaxTransferBytes = 16 * 1024 * 1024; // 16 MiB cap (driver-enforced)

    // -----------------------------------------------------------------
    // P/Invoke surface
    // -----------------------------------------------------------------
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref MEMORY_OPERATION lpInBuffer,
        int nInBufferSize,
        ref MEMORY_OPERATION lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);

    /// <summary>
    /// Mirror of <c>MEMORY_OPERATION</c> in <c>Shared/MemoryTool.h</c>.
    /// All fields are <c>ULONG64</c> on the C side and <see cref="ulong"/>
    /// here. The struct is passed both as input AND output buffer via
    /// METHOD_BUFFERED — the driver fills <see cref="BytesCopied"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct MEMORY_OPERATION
    {
        public ulong TargetPid;
        public ulong TargetAddress;
        public ulong BufferAddress;
        public ulong Size;
        public ulong BytesCopied;
    }

    // -----------------------------------------------------------------
    // State
    // -----------------------------------------------------------------
    private static readonly object SyncRoot = new();
    private static SafeFileHandle? _device;

    /// <summary>
    /// PID of the target process the driver should operate on.
    /// Set by <c>GameProcess</c> once the target process is located.
    /// </summary>
    public static ulong TargetPid { get; set; }

    /// <summary>
    /// True when the driver handle is open AND a target PID is set.
    /// Consumers should fall back to user-mode Win32 calls when false.
    /// </summary>
    public static bool IsAvailable
        => _device is { IsInvalid: false, IsClosed: false } && TargetPid != 0;

    // -----------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------
    /// <summary>
    /// Opens a handle to <c>\\.\DiskHelper</c>. Safe to call repeatedly.
    /// Returns <c>false</c> when the driver is not loaded.
    /// </summary>
    public static bool Initialize()
    {
        lock (SyncRoot)
        {
            if (_device is { IsInvalid: false, IsClosed: false })
            {
                return true;
            }

            var handle = CreateFileW(
                UsermodeDevicePath,
                GENERIC_READ | GENERIC_WRITE,
                0,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                handle.Dispose();
                return false;
            }

            _device = handle;
            return true;
        }
    }

    /// <summary>
    /// Releases the driver handle and clears the target PID.
    /// </summary>
    public static void Shutdown()
    {
        lock (SyncRoot)
        {
            _device?.Dispose();
            _device = null;
            TargetPid = 0;
        }
    }

    // -----------------------------------------------------------------
    // Public read / write surface
    // -----------------------------------------------------------------
    /// <summary>
    /// Reads a single unmanaged value from the target process.
    /// Returns <c>default</c> when the transfer fails.
    /// </summary>
    public static unsafe T Read<T>(IntPtr address) where T : unmanaged
    {
        T value = default;
        var size = sizeof(T);
        return TransferAt(IOCTL_DISK_READ_MEMORY, address, (IntPtr)(&value), size, out _)
            ? value
            : default;
    }

    /// <summary>
    /// Reads <paramref name="size"/> bytes into a freshly-allocated buffer.
    /// The returned buffer is always <paramref name="size"/> bytes long;
    /// trailing bytes are zero when the driver returned a short read.
    /// </summary>
    public static byte[] ReadBytes(IntPtr address, int size)
    {
        var buffer = new byte[size];
        ReadBytes(address, buffer);
        return buffer;
    }

    /// <summary>
    /// Reads bytes into a caller-provided span.
    /// Returns the number of bytes the driver reported as copied.
    /// </summary>
    public static int ReadBytes(IntPtr address, Span<byte> destination)
    {
        if (destination.IsEmpty) return 0;

        unsafe
        {
            fixed (byte* pBuf = destination)
            {
                return TransferAt(
                    IOCTL_DISK_READ_MEMORY,
                    address,
                    (IntPtr)pBuf,
                    destination.Length,
                    out var copied)
                    ? copied
                    : 0;
            }
        }
    }

    /// <summary>
    /// Writes a single unmanaged value into the target process.
    /// </summary>
    public static unsafe bool Write<T>(IntPtr address, T value) where T : unmanaged
    {
        var size = sizeof(T);
        return TransferAt(IOCTL_DISK_WRITE_MEMORY, address, (IntPtr)(&value), size, out _);
    }

    /// <summary>
    /// Writes the contents of <paramref name="source"/> into the target process.
    /// </summary>
    public static int WriteBytes(IntPtr address, ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty) return 0;

        unsafe
        {
            fixed (byte* pBuf = source)
            {
                return TransferAt(
                    IOCTL_DISK_WRITE_MEMORY,
                    address,
                    (IntPtr)pBuf,
                    source.Length,
                    out var copied)
                    ? copied
                    : 0;
            }
        }
    }

    // -----------------------------------------------------------------
    // Core dispatcher
    // -----------------------------------------------------------------
    private static bool TransferAt(
        uint ioctl,
        IntPtr targetAddress,
        IntPtr localBuffer,
        int size,
        out int bytesCopied)
    {
        bytesCopied = 0;

        if (size <= 0 || size > MaxTransferBytes)
        {
            return false;
        }

        var device = _device;
        var pid = TargetPid;
        if (device is null || device.IsInvalid || device.IsClosed || pid == 0)
        {
            return false;
        }

        var op = new MEMORY_OPERATION
        {
            TargetPid     = pid,
            TargetAddress = unchecked((ulong)targetAddress.ToInt64()),
            BufferAddress = unchecked((ulong)localBuffer.ToInt64()),
            Size          = (ulong)size,
            BytesCopied   = 0
        };

        var structSize = Unsafe.SizeOf<MEMORY_OPERATION>();

        var ok = DeviceIoControl(
            device,
            ioctl,
            ref op,
            structSize,
            ref op,
            structSize,
            out _,
            IntPtr.Zero);

        if (!ok)
        {
            // Surface the last error for callers that care; intentionally
            // do NOT throw — memory reads happen on hot paths and the
            // game freely contains transient invalid pointers.
            _ = Marshal.GetLastWin32Error();
            return false;
        }

        bytesCopied = (int)Math.Min(op.BytesCopied, (ulong)int.MaxValue);
        return bytesCopied == size;
    }

    // -----------------------------------------------------------------
    // CTL_CODE helper (matches the Windows macro)
    // -----------------------------------------------------------------
    private static uint CtlCode(uint deviceType, uint function, uint method, uint access)
        => (deviceType << 16) | (access << 14) | (function << 2) | method;

    /// <summary>
    /// Convenience wrapper used internally when callers want a hard
    /// failure rather than a silent zero read.
    /// </summary>
    internal static void ThrowLastError(string operation)
    {
        var code = Marshal.GetLastWin32Error();
        throw new Win32Exception(code, $"{operation} failed (Win32 0x{code:X8}).");
    }
}
