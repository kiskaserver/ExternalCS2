using System.Numerics;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Features;

public static class Radar
{
    private const float UnitsToMeters = 0.0254f;

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
        if (player == null) return;

        // Безопасно получаем снепшот, чтобы избежать гонки состояний
        RenderSnapshot snapshot;
        lock (player.RenderDataLock)
        {
            snapshot = player.RenderData;
        }
        
        if (snapshot == null) return;
        var entities = graphics.GameData.Entities;
        if (entities == null) return;

        float centerX = radarCfg.X + radarCfg.Size / 2f;
        float centerY = radarCfg.Y + radarCfg.Size / 2f;
        float radius = radarCfg.Size / 2f;

        // === Фон радара ===
        graphics.DrawCircleFilled(centerX, centerY, radius, ToUintColor(SkiaSharp.SKColors.Black.WithAlpha(160)));
        graphics.DrawCircleOutline(centerX, centerY, radius, ToUintColor(SkiaSharp.SKColors.White.WithAlpha(100)));

        // === Сетка и метки (статичные, север вверху) ===
        var gray = ToUintColor(SkiaSharp.SKColors.Gray.WithAlpha(80));
        graphics.DrawLine(gray, new Vector2(radarCfg.X, centerY), new Vector2(radarCfg.X + radarCfg.Size, centerY));
        graphics.DrawLine(gray, new Vector2(centerX, radarCfg.Y), new Vector2(centerX, radarCfg.Y + radarCfg.Size));

        var labelColor = ToUintColor(SkiaSharp.SKColors.White.WithAlpha(180));
        graphics.DrawText("N", centerX, radarCfg.Y + 10, labelColor, fontSize: 12);
        graphics.DrawText("S", centerX, radarCfg.Y + radarCfg.Size - 15, labelColor, fontSize: 12);
        graphics.DrawText("W", radarCfg.X + 10, centerY, labelColor, fontSize: 12);
        graphics.DrawText("E", radarCfg.X + radarCfg.Size - 15, centerY, labelColor, fontSize: 12);

        float pixelsPerMeter = radius / radarCfg.MaxDistance;

        // === Локальный игрок ===
        if (radarCfg.ShowLocalPlayer)
        {
            graphics.DrawCircleFilled(centerX, centerY, 5f, ToUintColor(SkiaSharp.SKColors.Cyan));
            graphics.DrawCircleOutline(centerX, centerY, 5f, ToUintColor(SkiaSharp.SKColors.White));

            // Стрелка направления взгляда локального игрока (всегда смотрит вверх на вращающемся радаре)
            graphics.DrawLine(ToUintColor(SkiaSharp.SKColors.White),
                new Vector2(centerX, centerY),
                new Vector2(centerX, centerY - 12f));
        }

        // === Получаем направление взгляда из снепшота ===
        var viewMatrix = snapshot.MatrixViewProjection;
        var forwardVector = new Vector2(viewMatrix.M13, viewMatrix.M23);
        float playerYawRad = MathF.Atan2(forwardVector.X, forwardVector.Y);
        
        // === КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: Вращаем мир в ту же сторону, что и игрок ===
        float cosYaw = MathF.Cos(playerYawRad);
        float sinYaw = MathF.Sin(playerYawRad);

        // === Все игроки ===
        foreach (var entity in entities)
        {
            if (!entity.IsAlive() || entity.AddressBase == player.AddressBase) continue;
            if (config.TeamCheck && entity.Team == player.Team) continue;

            // В CS2 система координат: X - "вперед/назад", Y - "вправо/влево"
            float relativeForward = entity.Position.X - snapshot.Position.X;
            float relativeRight = entity.Position.Y - snapshot.Position.Y;

            float distanceInUnits = MathF.Sqrt(relativeForward * relativeForward + relativeRight * relativeRight);
            float distanceInMeters = distanceInUnits * UnitsToMeters;
            if (distanceInMeters > radarCfg.MaxDistance) continue;

            // Вращаем координаты противника вместе с миром
            float rotatedX = (relativeForward * cosYaw) - (relativeRight * sinYaw);
            float rotatedY = (relativeForward * sinYaw) + (relativeRight * cosYaw);

            // Преобразуем в пиксели радара
            float x = centerX + rotatedX * UnitsToMeters * pixelsPerMeter;
            float y = centerY - rotatedY * UnitsToMeters * pixelsPerMeter; // Инвертируем Y для экрана

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

            // Направление взгляда противника
            if (radarCfg.ShowDirectionArrow && entity.ViewAngle.HasValue)
            {
                // Угол противника тоже нужно скорректировать
                float entityYawRad = (entity.ViewAngle.Value.Y + 90f) * MathF.PI / 180f;
                
                // Угол стрелки на радаре (учитываем вращение радара)
                float arrowYawRad = entityYawRad - playerYawRad;

                float arrowX = x + MathF.Sin(arrowYawRad) * 7f;
                float arrowY = y - MathF.Cos(arrowYawRad) * 7f;

                uint arrowColor = entity.IsVisible 
                    ? ToUintColor(SkiaSharp.SKColors.White) 
                    : ToUintColor(SkiaSharp.SKColors.Gray);

                graphics.DrawLine(arrowColor, new Vector2(x, y), new Vector2(arrowX, arrowY));
            }
        }
    }
}