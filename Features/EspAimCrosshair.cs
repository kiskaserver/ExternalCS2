using System.Numerics;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Features;

public static class EspAimCrosshair
{
    private static Vector3 _pointClip = Vector3.Zero;

    private static Vector3 GetPositionScreen(GameProcess gameProcess, Player player)
    {
        var screenSize = gameProcess.WindowRectangleClient.Size;
        var aspectRatio = (double)screenSize.Width / screenSize.Height;
        var fovY = GraphicsMath.DegreeToRadian((double)Player.Fov);
        var fovX = fovY * aspectRatio;
        var doPunch = player.ShotsFired > 0;
        var punchX = doPunch ? GraphicsMath.DegreeToRadian((double)player.AimPunchAngle.X * Offsets.WeaponRecoilScale) : 0;
        var punchY = doPunch ? GraphicsMath.DegreeToRadian((double)player.AimPunchAngle.Y * Offsets.WeaponRecoilScale) : 0;
        _pointClip = new Vector3
        (
            (float)(-punchY / fovX),
            (float)(-punchX / fovY),
            0
        );
        return player.MatrixViewport.Transform(_pointClip);
    }

    public static void Draw(ModernGraphics graphics)
    {
        var player = graphics.GameData.Player;
        if (player == null)
        {
            return;
        }

    var pointScreen = GetPositionScreen(graphics.GameProcess, player);
        Draw(graphics, new Vector2(pointScreen.X, pointScreen.Y));
    }

    private static void Draw(ModernGraphics graphics, Vector2 pointScreen)
    {
        const int crosshairRadius = 6;
        DrawCrosshair(graphics, pointScreen, crosshairRadius);
    }

    private static void DrawCrosshair(ModernGraphics graphics, Vector2 pointScreen, int radius)
    {
        // ModernGraphics.DrawLine expects a packed ARGB uint. Use 0xFFFFFFFF for white.
        const uint color = 0xFFFFFFFF;

        graphics.DrawLine(color, pointScreen - new Vector2(radius, 0),
            pointScreen + new Vector2(radius, 0));
        graphics.DrawLine(color, pointScreen - new Vector2(0, radius),
            pointScreen + new Vector2(0, radius));
    }
}