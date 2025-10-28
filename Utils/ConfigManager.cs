using System.IO;
using System.Text.Json;

namespace CS2GameHelper.Utils;

public class ConfigManager
{
    private const string ConfigFile = "config.json";

    // Основные флаги
    public bool AimBot { get; set; } = true;
    public bool BombTimer { get; set; } = true;
    public bool EspAimCrosshair { get; set; } = true;
    public bool SkeletonEsp { get; set; } = true;
    public bool TriggerBot { get; set; } = true;
    public Keys AimBotKey { get; set; } = Keys.LButton;
    public Keys TriggerBotKey { get; set; } = Keys.LMenu;
    public bool TeamCheck { get; set; } = true;
    public bool AimBotAutoShoot { get; set; } = true;

    // Вложенные настройки ESP
    public EspConfig Esp { get; set; } = new();

    // Вложенные классы конфигурации
    public class EspConfig
    {
        public BoxConfig Box { get; set; } = new();
        public RadarConfig Radar { get; set; } = new();

        public class BoxConfig
        {
            public bool Enabled { get; set; } = true;
            public bool ShowName { get; set; } = true;
            public bool ShowHealthBar { get; set; } = true;
            public bool ShowHealthText { get; set; } = true;
            public bool ShowDistance { get; set; } = true;
            public bool ShowWeaponIcon { get; set; } = true;
            public bool ShowArmor { get; set; } = true;
            public bool ShowVisibilityIndicator { get; set; } = true;
            public bool ShowFlags { get; set; } = true;
            public string EnemyColor { get; set; } = "FF8B0000";   // DarkRed
            public string TeamColor { get; set; } = "FF00008B";     // DarkBlue
            public string VisibleAlpha { get; set; } = "FF";
            public string InvisibleAlpha { get; set; } = "88";
        }

        public class RadarConfig
        {
            public bool Enabled { get; set; } = true;
            public int Size { get; set; } = 150;
            public int X { get; set; } = 50;
            public int Y { get; set; } = 50;
            public float MaxDistance { get; set; } = 100.0f;
            public bool ShowLocalPlayer { get; set; } = true;
            public bool ShowDirectionArrow { get; set; } = true;
            public string EnemyColor { get; set; } = "FFFF0000";   // Red
            public string TeamColor { get; set; } = "FF0000FF";    // Blue
            public string VisibleAlpha { get; set; } = "FF";
            public string InvisibleAlpha { get; set; } = "88";
        }
    }

    // Spectator list settings
    public SpectatorListConfig SpectatorList { get; set; } = new();

    public class SpectatorListConfig
    {
        public bool Enabled { get; set; } = true;
    }

    public static ConfigManager Load()
    {
        try
        {
            if (!File.Exists(ConfigFile))
            {
                var defaultConfig = Default();
                Save(defaultConfig);
                return defaultConfig;
            }

            var json = File.ReadAllText(ConfigFile);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            options.Converters.Add(new KeysJsonConverter()); // ← ДОБАВЛЕНО

            var config = JsonSerializer.Deserialize<ConfigManager>(json, options);

            config ??= Default();
            config.Esp ??= new EspConfig();
            config.Esp.Box ??= new EspConfig.BoxConfig();
            config.Esp.Radar ??= new EspConfig.RadarConfig();
            config.SpectatorList ??= new SpectatorListConfig();

            return config;
        }
        catch
        {
            var fallback = Default();
            Save(fallback);
            return fallback;
        }
    }

    public static void Save(ConfigManager options)
    {
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            jsonOptions.Converters.Add(new KeysJsonConverter()); // ← ДОБАВЛЕНО

            var json = JsonSerializer.Serialize(options, jsonOptions);
            File.WriteAllText(ConfigFile, json);
        }
        catch
        {
            // Игнор
        }
    }

    public static ConfigManager Default()
    {
        return new ConfigManager
        {
            // Основные флаги
            AimBot = true,
            AimBotAutoShoot = true,
            BombTimer = true,
            EspAimCrosshair = true,
            SkeletonEsp = true,
            TriggerBot = true,
            AimBotKey = Keys.LButton,
            TriggerBotKey = Keys.LMenu,
            TeamCheck = true,

            // <<< ИСПРАВЛЕНО: Явно создаем вложенные объекты с их значениями по умолчанию
            Esp = new EspConfig
            {
                Box = new EspConfig.BoxConfig
                {
                    Enabled = true,
                    ShowName = true,
                    ShowHealthBar = true,
                    ShowHealthText = true,
                    ShowDistance = true,
                    ShowWeaponIcon = true,
                    ShowArmor = true,
                    ShowVisibilityIndicator = true,
                    ShowFlags = true,
                    EnemyColor = "FF8B0000",
                    TeamColor = "FF00008B",
                    VisibleAlpha = "FF",
                    InvisibleAlpha = "88"
                },
                Radar = new EspConfig.RadarConfig
                {
                    Enabled = true,
                    Size = 150,
                    X = 50,
                    Y = 50,
                    MaxDistance = 100.0f,
                    ShowLocalPlayer = true,
                    ShowDirectionArrow = true,
                    EnemyColor = "FFFF0000",
                    TeamColor = "FF0000FF",
                    VisibleAlpha = "FF",
                    InvisibleAlpha = "88"
                }
            },
            SpectatorList = new SpectatorListConfig
            {
                Enabled = true
            }
        };
    }
}