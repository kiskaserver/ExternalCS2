using System.Runtime.InteropServices;
using System.Numerics;

namespace CS2GameHelper.Core.Data;

[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public Vector4 Position;
    public uint Color;
}