using System.IO;
using System.Text.Json;

namespace CS2GameHelper.Utils;

public class ConfigManager
{
    private const string ConfigFile = "config.json";

    // Основные флаги
    public bool AimBot { get; set; } = true;
    public bool BombTimer { get; set; } = true;
    // УДАЛЕНО: public bool EspAimCrosshair { get; set; } = true;
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
        public AimCrosshairConfig AimCrosshair { get; set; } = new();

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

        public class AimCrosshairConfig
        {
            public bool Enabled { get; set; } = true;
            public int Radius { get; set; } = 6;
            // ARGB hex string
            public string Color { get; set; } = "FFFFFFFF";
            // Recoil scale multiplier applied to punch angles
            public float RecoilScale { get; set; } = 2f;
        }
    }

    // Spectator list settings
    public SpectatorListConfig SpectatorList { get; set; } = new();

    public class SpectatorListConfig
    {
        public bool Enabled { get; set; } = true;
    }

    // Hit sound and on-screen hit text configuration
    public HitSoundConfig HitSound { get; set; } = new();

    public class HitSoundConfig
    {
        public bool Enabled { get; set; } = true;
        // Colors are ARGB hex strings like "FFFF0000" (opaque red)
        public string HitColor { get; set; } = "FFFFFFFF"; // white
        public string HeadshotColor { get; set; } = "FFFFD700"; // gold
        // Text shown on screen for hit and headshot
        public string HitText { get; set; } = "HIT";
        public string HeadshotText { get; set; } = "HEADSHOT";
        // Paths to sound files (can be absolute or relative to app base dir)
        public string HitSoundFile { get; set; } = "assets/sounds/hit.wav";
        public string HeadshotSoundFile { get; set; } = "assets/sounds/headshot.wav";
        // Advanced settings
        public int HeadshotDamageThreshold { get; set; } = 100;   // урон ≥ 100 → хедшот
        public double TextDurationSeconds { get; set; } = 1.5;    // длительность текста в секундах
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
            options.Converters.Add(new KeysJsonConverter());

            var config = JsonSerializer.Deserialize<ConfigManager>(json, options);

            config ??= Default();
            config.Esp ??= new EspConfig();
            config.Esp.Box ??= new EspConfig.BoxConfig();
            config.Esp.Radar ??= new EspConfig.RadarConfig();
            config.Esp.AimCrosshair ??= new EspConfig.AimCrosshairConfig();
            config.SpectatorList ??= new SpectatorListConfig();
            config.HitSound ??= new HitSoundConfig();

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
            jsonOptions.Converters.Add(new KeysJsonConverter());

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
            // УДАЛЕНО: EspAimCrosshair = true,
            SkeletonEsp = true,
            TriggerBot = true,
            AimBotKey = Keys.LButton,
            TriggerBotKey = Keys.LMenu,
            TeamCheck = true,

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
                },
                AimCrosshair = new EspConfig.AimCrosshairConfig
                {
                    Enabled = true,
                    Radius = 6,
                    Color = "FFFFFFFF",
                    RecoilScale = 2f
                }
            },
            SpectatorList = new SpectatorListConfig
            {
                Enabled = true
            },
            HitSound = new HitSoundConfig
            {
                Enabled = true,
                HitColor = "FFFFFFFF",
                HeadshotColor = "FFFFD700",
                HitText = "HIT",
                HeadshotText = "HEADSHOT",
                HitSoundFile = "assets/sounds/hit.wav",
                HeadshotSoundFile = "assets/sounds/headshot.wav",
                HeadshotDamageThreshold = 100,
                TextDurationSeconds = 1.5
            }
        };
    }
}