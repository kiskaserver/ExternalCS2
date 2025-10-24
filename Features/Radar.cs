using System.Numerics;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Features;

public static class Radar
{
    // 1 unit = 1/39.37 метра
    private const float UnitsToMeters = 1f / 39.37f;

    private static uint ToUintColor(SkiaSharp.SKColor color)
    {
        return ((uint)color.Alpha << 24) |
               ((uint)color.Red << 16) |
               ((uint)color.Green << 8) |
               color.Blue;
    }

    public static void Draw(ModernGraphics graphics)
    {
        var config = ConfigManager.Load();
        var radarCfg = config.Esp.Radar;
        if (!radarCfg.Enabled) return;

        var player = graphics.GameData.Player;
        var entities = graphics.GameData.Entities;
        if (player == null || entities == null) return;

        float centerX = radarCfg.X + radarCfg.Size / 2f;
        float centerY = radarCfg.Y + radarCfg.Size / 2f;
        float radius = radarCfg.Size / 2f;

        // === Фон радара ===
        graphics.DrawCircleFilled(centerX, centerY, radius, ToUintColor(SkiaSharp.SKColors.Black.WithAlpha(160)));
        graphics.DrawCircleOutline(centerX, centerY, radius, ToUintColor(SkiaSharp.SKColors.White.WithAlpha(100)));

        // === Сетка и метки ===
        var gray = ToUintColor(SkiaSharp.SKColors.Gray.WithAlpha(80));
        graphics.DrawLine(gray, new Vector2(radarCfg.X, centerY), new Vector2(radarCfg.X + radarCfg.Size, centerY));
        graphics.DrawLine(gray, new Vector2(centerX, radarCfg.Y), new Vector2(centerX, radarCfg.Y + radarCfg.Size));

        var labelColor = ToUintColor(SkiaSharp.SKColors.White.WithAlpha(180));
        graphics.DrawText("N", centerX, radarCfg.Y + 10, labelColor, fontSize: 12);
        graphics.DrawText("S", centerX, radarCfg.Y + radarCfg.Size - 15, labelColor, fontSize: 12);
        graphics.DrawText("W", radarCfg.X + 10, centerY, labelColor, fontSize: 12);
        graphics.DrawText("E", radarCfg.X + radarCfg.Size - 15, centerY, labelColor, fontSize: 12);

        // === Локальный игрок ===
        if (radarCfg.ShowLocalPlayer)
        {
            graphics.DrawCircleFilled(centerX, centerY, 5f, ToUintColor(SkiaSharp.SKColors.Cyan));
            graphics.DrawCircleOutline(centerX, centerY, 5f, ToUintColor(SkiaSharp.SKColors.White));

            if (player.ViewAngle.HasValue)
            {
                float yawRad = player.ViewAngle.Value.Y * MathF.PI / 180f;
                float endX = centerX + MathF.Sin(yawRad) * 12f;
                float endY = centerY - MathF.Cos(yawRad) * 12f;
                graphics.DrawLine(ToUintColor(SkiaSharp.SKColors.White),
                    new Vector2(centerX, centerY),
                    new Vector2(endX, endY));
            }
        }

        // === Все игроки (включая тиммейтов, если разрешено) ===
        foreach (var entity in entities)
        {
            if (!entity.IsAlive() || entity.AddressBase == player.AddressBase) continue;

            // 🔥 ВАЖНО: используем TeamCheck из ГЛОБАЛЬНОГО конфига, как в ESP
            if (config.TeamCheck && entity.Team == player.Team) continue;

            // === ПОЗИЦИЯ: правильно определяем right и forward ===
            float right = entity.Position.X - player.Position.X;   // X = влево/вправо
            float forward = entity.Position.Y - player.Position.Y; // Y = вперёд/назад

            float distanceInUnits = MathF.Sqrt(right * right + forward * forward);
            float distanceInMeters = distanceInUnits * UnitsToMeters;
            if (distanceInMeters > radarCfg.MaxDistance) continue;

            // Масштаб в пикселях на метр
            float pixelsPerMeter = radius / radarCfg.MaxDistance;

            // Преобразуем в пиксели радара:
            //   - right → X (без инверсии)
            //   - forward → Y (инвертируем, т.к. вперёд = вверх = -Y экрана)
            float x = centerX + (right * UnitsToMeters) * pixelsPerMeter;
            float y = centerY - (forward * UnitsToMeters) * pixelsPerMeter;

            // Цвет
            string colorHex = entity.Team == Team.Terrorists 
                ? radarCfg.EnemyColor 
                : radarCfg.TeamColor;
            byte alpha = entity.IsVisible 
                ? Convert.ToByte(radarCfg.VisibleAlpha, 16) 
                : Convert.ToByte(radarCfg.InvisibleAlpha, 16);
            uint baseColor = Convert.ToUInt32(colorHex, 16);
            uint playerColor = ((uint)alpha << 24) | (baseColor & 0x00FFFFFFu);

            // Игрок
            graphics.DrawCircleFilled(x, y, 3.5f, playerColor);
            graphics.DrawCircleOutline(x, y, 3.5f, ToUintColor(SkiaSharp.SKColors.White.WithAlpha(200)));

            // Направление взгляда
            if (radarCfg.ShowDirectionArrow && entity.ViewAngle.HasValue)
            {
                float yawRad = entity.ViewAngle.Value.Y * MathF.PI / 180f;
                float arrowX = x + MathF.Sin(yawRad) * 7f;
                float arrowY = y - MathF.Cos(yawRad) * 7f;

                uint arrowColor = entity.IsVisible 
                    ? ToUintColor(SkiaSharp.SKColors.White) 
                    : ToUintColor(SkiaSharp.SKColors.Gray);

                graphics.DrawLine(arrowColor, new Vector2(x, y), new Vector2(arrowX, arrowY));
            }
        }
    }
}