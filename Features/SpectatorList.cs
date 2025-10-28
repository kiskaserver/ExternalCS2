using System.Collections.Generic;
using System.Linq;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;
using SkiaSharp;

namespace CS2GameHelper.Features;

/// <summary>
/// Отображает список мертвых игроков (спектаторов), которые наблюдают за вами.
/// </summary>
public static class SpectatorList
{
    public static void Draw(ModernGraphics graphics)
    {
        var config = ConfigManager.Load();
        if (!config.SpectatorList.Enabled) return;

        var player = graphics.GameData.Player;
        var entities = graphics.GameData.Entities;
        if (player == null || entities == null) return;

        var spectatorNames = new List<string>();

        foreach (var entity in entities)
        {
            // <<< КЛЮЧЕВАЯ ЛОГИКА: Проверяем, что это мертвый игрок
            if (entity.IsAlive() || entity.AddressBase == player.AddressBase) continue;
    
            var spectatorTarget = entity.ObserverTarget;
            
            if (spectatorTarget == player.AddressBase)
            {
                spectatorNames.Add(entity.Name ?? "UNKNOWN");
            }
        }

        // --- Отрисовка списка ---
        if (spectatorNames.Any())
        {
            // Начальная позиция для текста, прямо под таймером бомбы
            float startY = 560; 
            float startX = 10;
            int spacing = 16;

            // Заголовок
            graphics.AddText("Spectators:", startX, startY, SKColors.Gold, 14);

            // Список имен
            for (int i = 0; i < spectatorNames.Count; i++)
            {
                graphics.AddText($"- {spectatorNames[i]}", startX, startY + (i + 1) * spacing, SKColors.WhiteSmoke, 12);
            }
        }
    }
}