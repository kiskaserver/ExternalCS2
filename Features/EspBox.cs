using System.Numerics;
using System.Collections.Generic;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Features;

/// <summary>
/// Вспомогательные цвета в формате ARGB (0xAARRGGBB).
/// </summary>
internal static class EspColor
{
    public const uint White = 0xFFFFFFFF;
    public const uint Green = 0xFF00FF00;
    public const uint Black = 0xFF000000;
    public const uint Red = 0xFFFF0000;
    public const uint Yellow = 0xFFFFFF00;
    public const uint Orange = 0xFFFFA500; // Цвет для C4
    public const uint Gray = 0xFF808080;
}

/// <summary>
/// Отображает ESP-боксы вокруг игроков с полной кастомизацией через конфиг.
/// </summary>
public static class EspBox
{
    private const int OutlineThickness = 1;
    private const float UnitsToMeters = 0.0254f;

    private static readonly Dictionary<string, string> GunIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        // Ножи
        ["knife"] = "[", ["knife_t"] = "[", ["knife_ct"] = "]", ["bayonet"] = "p",
        ["flipknife"] = "q", ["gutknife"] = "r", ["karambit"] = "s", ["m9bayonet"] = "t",
        ["tacticalknife"] = "u", ["butterflyknife"] = "v", ["falchionknife"] = "w",
        ["shadowdaggers"] = "x", ["paracordknife"] = "y", ["survivalknife"] = "z",
        ["ursusknife"] = "{", ["navajaknife"] = "|", ["nomadknife"] = "}",
        ["stilettoknife"] = "~", ["talonknife"] = "⌂", ["classicknife"] = "Ç",

        // Пистолеты
        ["deagle"] = "A", ["elite"] = "B", ["fiveseven"] = "C", ["glock"] = "D",
        ["hkp2000"] = "E", ["p250"] = "F", ["usp_silencer"] = "G", ["tec9"] = "H",
        ["cz75a"] = "I", ["revolver"] = "J",

        // SMG
        ["mac10"] = "K", ["mp9"] = "L", ["mp7"] = "M", ["ump45"] = "N",
        ["p90"] = "O", ["bizon"] = "P",

        // Штурмовые
        ["ak47"] = "Q", ["aug"] = "R", ["famas"] = "S", ["galilar"] = "T",
        ["m4a1"] = "U", ["m4a1_silencer"] = "V", ["sg556"] = "W",

        // Снайперские
        ["awp"] = "X", ["g3sg1"] = "Y", ["scar20"] = "Z", ["ssg08"] = "a",

        // Дробовики
        ["mag7"] = "b", ["nova"] = "c", ["sawedoff"] = "d", ["xm1014"] = "e",

        // Пулемёты
        ["m249"] = "f", ["negev"] = "g",

        // Прочее
        ["taser"] = "h", ["c4"] = "o",

        // Гранаты
        ["flashbang"] = "i", ["hegrenade"] = "j", ["smokegrenade"] = "k",
        ["molotov"] = "l", ["decoy"] = "m", ["incgrenade"] = "n"
    };

    // <<< НОВОЕ: Иконки для статусов в формате Unicode
    private static readonly Dictionary<string, string> StatusIcons = new()
    {
        ["Flashed"] = "💡",
        ["Scoped"] = "🔎",
        ["Defusing"] = "💣",
        ["Air"] = "🪂",
        ["Running"] = "🏃",
        ["Walking"] = "🚶"
    };

    // <<< НОВЫЙ ВСПОМОГАТЕЛЬНЫЙ МЕТОД ДЛЯ ЦЕНТРИРОВАНИЯ ТЕКСТА
    private static void DrawCenteredText(ModernGraphics graphics, string text, float centerX, float y, uint color, float fontSize = 12, bool useCustomFont = false)
    {
        var textSize = graphics.MeasureText(text, fontSize, useCustomFont);
        float textWidth = textSize.X;
        float textX = centerX - textWidth / 2f;
        graphics.DrawText(text, textX, y, color, fontSize, useCustomFont);
    }

    public static void Draw(ModernGraphics graphics)
    {

        var fullConfig = ConfigManager.Load();
        var espConfig = fullConfig.Esp.Box;

        if (!espConfig.Enabled) return;

        var player = graphics.GameData.Player;
        var entities = graphics.GameData.Entities;
        if (player == null || entities == null) return;

        foreach (var entity in entities)
        {
            if (!entity.IsAlive() || entity.AddressBase == player.AddressBase) continue;
            if (fullConfig.TeamCheck && entity.Team == player.Team) continue;

            var bbox = GetEntityBoundingBox(player, entity);
            if (bbox == null) continue;

            DrawEntityEsp(graphics, player, entity, bbox.Value, espConfig);
        }
    }

    private static void DrawEntityEsp(
        ModernGraphics graphics,
        Player localPlayer,
        Entity entity,
        (Vector2 TopLeft, Vector2 BottomRight) bbox,
        ConfigManager.EspConfig.BoxConfig config)
    {
        var (topLeft, bottomRight) = bbox;
        if (topLeft.X >= bottomRight.X || topLeft.Y >= bottomRight.Y) return;

        // === Цвет с учётом видимости и команды ===
        bool isVisible = entity.IsVisible;
        string colorHex = entity.Team == Team.Terrorists 
            ? config.EnemyColor 
            : config.TeamColor;

        byte alpha = isVisible
            ? Convert.ToByte(config.VisibleAlpha, 16)
            : Convert.ToByte(config.InvisibleAlpha, 16);

        uint baseColor = Convert.ToUInt32(colorHex, 16);
        uint boxColor = SetAlpha(baseColor, alpha);

        // === Бокс ===
        float width = bottomRight.X - topLeft.X;
        float height = bottomRight.Y - topLeft.Y;
        graphics.DrawRectOutline(topLeft.X, topLeft.Y, width, height, boxColor);

        float textY = topLeft.Y - 16;
        float centerX = (topLeft.X + bottomRight.X) / 2f;

        // === Имя ===
        if (config.ShowName)
        {
            string rawName = entity.Name ?? "UNKNOWN";
            string name = rawName.Length > 0 ? rawName.Substring(0, rawName.Length - 1) : "UNKNOWN";
            DrawCenteredText(graphics, name, centerX, textY, EspColor.White);
            textY += 14;
        }

        // <<< ИЗМЕНЕНО: Проверяем, несет ли игрок бомбу по имени оружия
        bool hasBomb = !string.IsNullOrEmpty(entity.CurrentWeaponName) && entity.CurrentWeaponName.Contains("c4", StringComparison.OrdinalIgnoreCase);
        if (hasBomb)
        {
            string bombText = "💣 C4";
            DrawCenteredText(graphics, bombText, centerX, textY, EspColor.Orange);
            textY += 14;
        }

        // === Дистанция ===
        if (config.ShowDistance)
        {
            float distance = Vector3.Distance(localPlayer.Position, entity.Position) * UnitsToMeters;
            string distText = $"{distance:0}m";
            DrawCenteredText(graphics, distText, centerX, textY, EspColor.White);
            textY += 14;
        }

        // === Полоска здоровья ===
        if (config.ShowHealthBar)
        {
            float healthBarLeft = topLeft.X - 8f;
            float healthBarHeight = height;
            float healthBarWidth = 4f;
            
            float healthPercentage = Math.Clamp(entity.Health / 100f, 0f, 1f);
            float filledHeight = healthBarHeight * healthPercentage;
            float filledTopY = topLeft.Y + (healthBarHeight - filledHeight);
            
            uint healthBarColor = entity.Health > 60 ? EspColor.Green : 
                                   entity.Health > 30 ? EspColor.Yellow : EspColor.Red;

            graphics.DrawRect(healthBarLeft, topLeft.Y, healthBarWidth, healthBarHeight, 0x80000000);
            graphics.DrawRect(healthBarLeft, filledTopY, healthBarWidth, filledHeight, healthBarColor);
            graphics.DrawRectOutline(healthBarLeft - 1, topLeft.Y - 1, healthBarWidth + 2, healthBarHeight + 2, EspColor.Black);
        }

        // === Текст здоровья ===
        if (config.ShowHealthText)
        {
            string healthText = entity.Health.ToString();
            int healthX = (int)(bottomRight.X + 2);
            int healthY = (int)(topLeft.Y + height / 2f);
            graphics.DrawText(healthText, healthX, healthY, EspColor.White);
        }

        // === Броня / Шлем ===
        if (config.ShowArmor && entity.Armor > 0)
        {
            string armorText = entity.HasHelmet ? $"🛡{entity.Armor}" : $"🥚{entity.Armor}";
            int armorX = (int)(topLeft.X - 12);
            int armorY = (int)(bottomRight.Y - 12);
            graphics.DrawText(armorText, armorX, armorY, EspColor.White);
        }

        // === Иконка оружия ===
        if (config.ShowWeaponIcon && !string.IsNullOrEmpty(entity.CurrentWeaponName))
        {
            string icon = GetWeaponIcon(entity.CurrentWeaponName);
            if (!string.IsNullOrEmpty(icon))
            {
                int weaponY = (int)(bottomRight.Y + 2);
                bool useCustom = graphics is ModernGraphics mg && mg.IsUndefeatedFontLoaded;
                DrawCenteredText(graphics, icon, centerX, weaponY, EspColor.White, 14, useCustom);
            }
        }

        // === Статусы (с иконками) ===
        if (config.ShowFlags)
        {
            int flagX = (int)(bottomRight.X + 5);
            int flagY = (int)topLeft.Y;
            int spacing = 16;
            int line = 0;

            if (entity.FlashAlpha > 7)
            {
                graphics.DrawText($"{StatusIcons["Flashed"]} Flashed", flagX, flagY + line * spacing, EspColor.Yellow);
                line++;
            }

            if (entity.IsInScope == 1)
            {
                graphics.DrawText($"{StatusIcons["Scoped"]} Scoped", flagX, flagY + line * spacing, EspColor.White);
                line++;
            }

            if (entity.IsDefusing)
            {
                graphics.DrawText($"{StatusIcons["Defusing"]} Defusing", flagX, flagY + line * spacing, EspColor.Red);
                line++;
            }

            if (!entity.Flags.HasFlag(EntityFlags.OnGround))
            {
                graphics.DrawText($"{StatusIcons["Air"]} Air", flagX, flagY + line * spacing, EspColor.Gray);
                line++;
            }

            float speed = entity.Velocity.Length();
            if (speed > 200f)
            {
                graphics.DrawText($"{StatusIcons["Running"]} Running", flagX, flagY + line * spacing, EspColor.White);
                line++;
            }
            else if (speed > 10f)
            {
                graphics.DrawText($"{StatusIcons["Walking"]} Walking", flagX, flagY + line * spacing, EspColor.Gray);
                line++;
            }
        }
    }

    private static string GetWeaponIcon(string? weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return string.Empty;
        string cleanName = weaponName.Replace("weapon_", "", StringComparison.OrdinalIgnoreCase);
        return GunIcons.GetValueOrDefault(cleanName, "?");
    }

    private static uint SetAlpha(uint color, byte alpha)
    {
        return ((uint)alpha << 24) | (color & 0x00FFFFFFu);
    }

    private static (Vector2, Vector2)? GetEntityBoundingBox(Player player, Entity entity)
    {
        const float BasePadding = 5.0f;
        var minPos = new Vector2(float.MaxValue, float.MaxValue);
        var maxPos = new Vector2(float.MinValue, float.MinValue);

        var matrix = player.MatrixViewProjectionViewport;
        if (entity.BonePos == null || entity.BonePos.Count == 0)
            return null;

        bool anyValid = false;
        foreach (var bone in entity.BonePos.Values)
        {
            var projected = matrix.Transform(bone);
            if (projected.Z >= 1 || projected.X < 0 || projected.Y < 0)
                continue;

            anyValid = true;
            minPos.X = Math.Min(minPos.X, projected.X);
            minPos.Y = Math.Min(minPos.Y, projected.Y);
            maxPos.X = Math.Max(maxPos.X, projected.X);
            maxPos.Y = Math.Max(maxPos.Y, projected.Y);
        }

        if (!anyValid)
            return null;

        var sizeMultiplier = 2f - (entity.Health / 100f);
        var padding = new Vector2(BasePadding * sizeMultiplier);

        return (minPos - padding, maxPos + padding);
    }
}