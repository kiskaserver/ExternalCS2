using System.Numerics;
using System.Drawing;

namespace CS2GameHelper.Graphics;

public static class GraphicsMath
{
    public static Vector3 GetVectorFromEulerAngles(double phi, double theta)
    {
        return Vector3.Normalize(new Vector3
        (
            (float)(Math.Cos(phi) * Math.Cos(theta)),
            (float)(Math.Cos(phi) * Math.Sin(theta)),
            (float)-Math.Sin(phi)
        ));
    }

    public static Matrix4x4 GetMatrixViewport(Size screenSize)
    {
        var viewport = new ViewportInfo
        {
            X = 0,
            Y = 0,
            Width = screenSize.Width,
            Height = screenSize.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        
        return GetMatrixViewport(viewport);
    }

    private static Matrix4x4 GetMatrixViewport(ViewportInfo viewport)
    {
        return new Matrix4x4(
            viewport.Width * 0.5f, 0, 0, 0,
            0, -viewport.Height * 0.5f, 0, 0,
            0, 0, viewport.MaxDepth - viewport.MinDepth, 0,
            viewport.X + viewport.Width * 0.5f,
            viewport.Y + viewport.Height * 0.5f,
            viewport.MinDepth,
            1.0f
        );
    }

    public static Vector3 Transform(this Matrix4x4 matrix, Vector3 value)
    {
        var wInv = 1.0 / (matrix.M14 * value.X + matrix.M24 * value.Y +
                          matrix.M34 * value.Z + matrix.M44);

        return new Vector3(
            (float)((matrix.M11 * value.X + matrix.M21 * value.Y +
                    matrix.M31 * value.Z + matrix.M41) * wInv),
            (float)((matrix.M12 * value.X + matrix.M22 * value.Y +
                    matrix.M32 * value.Z + matrix.M42) * wInv),
            (float)((matrix.M13 * value.X + matrix.M23 * value.Y +
                    matrix.M33 * value.Z + matrix.M43) * wInv)
        );
    }

    public static double DegreeToRadian(this double angle)
    {
        return Math.PI * angle / 180.0;
    }

    public static double RadianToDegree(this double angle)
    {
        return angle * (180.0 / Math.PI);
    }

    public static float DegreeToRadian(this float angle)
    {
        return (float)(Math.PI * angle / 180.0);
    }

    public static float RadianToDegree(this float angle)
    {
        return (float)(angle * (180.0 / Math.PI));
    }

    public static Vector3 GetNormalized(this Vector3 vector)
    {
        return Vector3.Normalize(vector);
    }

    public static float GetAngleTo(this Vector3 from, Vector3 to)
    {
        var dot = Vector3.Dot(Vector3.Normalize(from), Vector3.Normalize(to));
        dot = Math.Clamp(dot, -1.0f, 1.0f);
        return MathF.Acos(dot);
    }

    public static float GetSignedAngleTo(this Vector3 from, Vector3 to, Vector3 axis)
    {
        var angle = GetAngleTo(from, to);
        var cross = Vector3.Cross(from, to);
        var sign = Vector3.Dot(cross, axis) < 0 ? -1.0f : 1.0f;
        return angle * sign;
    }
}

public struct ViewportInfo
{
    public float X;
    public float Y;
    public float Width;
    public float Height;
    public float MinDepth;
    public float MaxDepth;
}