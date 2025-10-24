using System.Runtime.InteropServices;

namespace CS2GameHelper.Core.Data;

[StructLayout(LayoutKind.Sequential)]
public struct Rect
{
    public int Left, Top, Right, Bottom;
}