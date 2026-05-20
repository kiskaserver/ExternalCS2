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
    public Keys MenuToggleKey { get; set; } = Keys.Insert;
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

            // ---- v2.0: FOV Circle ----
            // Draws a circle around the screen center representing the aim FOV radius.
            public bool ShowFovCircle { get; set; } = false;
            // Radius in pixels (screen-space). Independent of game FOV for simplicity & predictability.
            public int FovCircleRadius { get; set; } = 120;
            // ARGB hex
            public string FovCircleColor { get; set; } = "80FFFFFF";
        }
    }

    // v2.0: Vote Teller
    public VoteTellerConfig VoteTeller { get; set; } = new();

    public class VoteTellerConfig
    {
        public bool Enabled { get; set; } = true;
        // ARGB hex strings
        public string ColorT { get; set; } = "FFFF8C00";   // OrangeRed-ish
        public string ColorCT { get; set; } = "FF00BFFF";  // DeepSkyBlue
        public string ColorAll { get; set; } = "FFFFFFFF";
        public int X { get; set; } = 10;
        public int Y { get; set; } = 350;
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
            config.VoteTeller ??= new VoteTellerConfig();

            return config;
        }
        catch
        {
            var fallback = Default();
            Save(fallback);
            return fallback;
        }
    }

    /// <summary>v2.0: reload config from disk and copy values into this instance. Returns true on success.</summary>
    public bool ReloadInPlace()
    {
        try
        {
            var fresh = Load();
            CopyFrom(fresh);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>v2.0: reset all values to defaults and persist.</summary>
    public bool ResetDefaults()
    {
        try
        {
            var def = Default();
            CopyFrom(def);
            Save(this);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>v2.0: save self to disk.</summary>
    public bool SaveCurrent()
    {
        try { Save(this); return true; } catch { return false; }
    }

    private void CopyFrom(ConfigManager other)
    {
        AimBot = other.AimBot;
        AimBotAutoShoot = other.AimBotAutoShoot;
        BombTimer = other.BombTimer;
        SkeletonEsp = other.SkeletonEsp;
        TriggerBot = other.TriggerBot;
        AimBotKey = other.AimBotKey;
        TriggerBotKey = other.TriggerBotKey;
        MenuToggleKey = other.MenuToggleKey;
        TeamCheck = other.TeamCheck;

        Esp ??= new EspConfig();
        Esp.Box ??= new EspConfig.BoxConfig();
        Esp.Radar ??= new EspConfig.RadarConfig();
        Esp.AimCrosshair ??= new EspConfig.AimCrosshairConfig();

        // Box
        Esp.Box.Enabled = other.Esp.Box.Enabled;
        Esp.Box.ShowName = other.Esp.Box.ShowName;
        Esp.Box.ShowHealthBar = other.Esp.Box.ShowHealthBar;
        Esp.Box.ShowHealthText = other.Esp.Box.ShowHealthText;
        Esp.Box.ShowDistance = other.Esp.Box.ShowDistance;
        Esp.Box.ShowWeaponIcon = other.Esp.Box.ShowWeaponIcon;
        Esp.Box.ShowArmor = other.Esp.Box.ShowArmor;
        Esp.Box.ShowVisibilityIndicator = other.Esp.Box.ShowVisibilityIndicator;
        Esp.Box.ShowFlags = other.Esp.Box.ShowFlags;
        Esp.Box.EnemyColor = other.Esp.Box.EnemyColor;
        Esp.Box.TeamColor = other.Esp.Box.TeamColor;
        Esp.Box.VisibleAlpha = other.Esp.Box.VisibleAlpha;
        Esp.Box.InvisibleAlpha = other.Esp.Box.InvisibleAlpha;

        // Radar
        Esp.Radar.Enabled = other.Esp.Radar.Enabled;
        Esp.Radar.Size = other.Esp.Radar.Size;
        Esp.Radar.X = other.Esp.Radar.X;
        Esp.Radar.Y = other.Esp.Radar.Y;
        Esp.Radar.MaxDistance = other.Esp.Radar.MaxDistance;
        Esp.Radar.ShowLocalPlayer = other.Esp.Radar.ShowLocalPlayer;
        Esp.Radar.ShowDirectionArrow = other.Esp.Radar.ShowDirectionArrow;
        Esp.Radar.EnemyColor = other.Esp.Radar.EnemyColor;
        Esp.Radar.TeamColor = other.Esp.Radar.TeamColor;
        Esp.Radar.VisibleAlpha = other.Esp.Radar.VisibleAlpha;
        Esp.Radar.InvisibleAlpha = other.Esp.Radar.InvisibleAlpha;

        // AimCrosshair + FovCircle
        Esp.AimCrosshair.Enabled = other.Esp.AimCrosshair.Enabled;
        Esp.AimCrosshair.Radius = other.Esp.AimCrosshair.Radius;
        Esp.AimCrosshair.Color = other.Esp.AimCrosshair.Color;
        Esp.AimCrosshair.RecoilScale = other.Esp.AimCrosshair.RecoilScale;
        Esp.AimCrosshair.ShowFovCircle = other.Esp.AimCrosshair.ShowFovCircle;
        Esp.AimCrosshair.FovCircleRadius = other.Esp.AimCrosshair.FovCircleRadius;
        Esp.AimCrosshair.FovCircleColor = other.Esp.AimCrosshair.FovCircleColor;

        SpectatorList ??= new SpectatorListConfig();
        SpectatorList.Enabled = other.SpectatorList.Enabled;

        HitSound ??= new HitSoundConfig();
        HitSound.Enabled = other.HitSound.Enabled;
        HitSound.HitColor = other.HitSound.HitColor;
        HitSound.HeadshotColor = other.HitSound.HeadshotColor;
        HitSound.HitText = other.HitSound.HitText;
        HitSound.HeadshotText = other.HitSound.HeadshotText;
        HitSound.HitSoundFile = other.HitSound.HitSoundFile;
        HitSound.HeadshotSoundFile = other.HitSound.HeadshotSoundFile;
        HitSound.HeadshotDamageThreshold = other.HitSound.HeadshotDamageThreshold;
        HitSound.TextDurationSeconds = other.HitSound.TextDurationSeconds;

        VoteTeller ??= new VoteTellerConfig();
        VoteTeller.Enabled = other.VoteTeller.Enabled;
        VoteTeller.ColorT = other.VoteTeller.ColorT;
        VoteTeller.ColorCT = other.VoteTeller.ColorCT;
        VoteTeller.ColorAll = other.VoteTeller.ColorAll;
        VoteTeller.X = other.VoteTeller.X;
        VoteTeller.Y = other.VoteTeller.Y;
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
            MenuToggleKey = Keys.Insert,
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