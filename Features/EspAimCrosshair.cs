using System.Numerics;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Features;

public static class EspAimCrosshair
{
    private static Vector3 _pointClip = Vector3.Zero;

    private static Vector3 GetPositionScreen(GameProcess gameProcess, Player player, float recoilScale)
    {
        var screenSize = gameProcess.WindowRectangleClient.Size;
        var aspectRatio = (double)screenSize.Width / screenSize.Height;
        var fovY = GraphicsMath.DegreeToRadian((double)Player.Fov);
        var fovX = fovY * aspectRatio;
        var doPunch = player.ShotsFired > 0;
        var punchX = doPunch ? GraphicsMath.DegreeToRadian((double)player.AimPunchAngle.X * recoilScale) : 0;
        var punchY = doPunch ? GraphicsMath.DegreeToRadian((double)player.AimPunchAngle.Y * recoilScale) : 0;
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
            return;

        var cfg = ConfigManager.Load();
        var aimCfg = cfg?.Esp?.AimCrosshair ?? new ConfigManager.EspConfig.AimCrosshairConfig();
        if (!aimCfg.Enabled)
            return;

        var pointScreen = GetPositionScreen(graphics.GameProcess, player, aimCfg.RecoilScale);
        Draw(graphics, new Vector2(pointScreen.X, pointScreen.Y), aimCfg);
    }

    private static void Draw(ModernGraphics graphics, Vector2 pointScreen, ConfigManager.EspConfig.AimCrosshairConfig aimCfg)
    {
        var crosshairRadius = aimCfg.Radius;
        DrawCrosshair(graphics, pointScreen, crosshairRadius, aimCfg.Color);
    }

    private static void DrawCrosshair(ModernGraphics graphics, Vector2 pointScreen, int radius, string hexColor)
    {
        uint color = ParseColorHex(hexColor);

        graphics.DrawLine(color, pointScreen - new Vector2(radius, 0),
            pointScreen + new Vector2(radius, 0));
        graphics.DrawLine(color, pointScreen - new Vector2(0, radius),
            pointScreen + new Vector2(0, radius));
    }

    private static uint ParseColorHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return 0xFFFFFFFFu;
        hex = hex.Trim();
        if (hex.StartsWith("#")) hex = hex.Substring(1);
        if (hex.StartsWith("0x") || hex.StartsWith("0X")) hex = hex.Substring(2);
        if (hex.Length > 8) hex = hex.Substring(hex.Length - 8);
        try { return Convert.ToUInt32(hex, 16); }
        catch { return 0xFFFFFFFFu; }
    }
}