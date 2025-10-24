# CONFIG_REFERENCE.md

Purpose
-------
This document is a concise reference for the project's `config.json`. It lists every configuration field, its JSON type, the C# property it maps to, the default value, allowed values or ranges (where applicable), and where in the source code the field is used.

All primary config classes live in: `Utils/ConfigManager.cs` (class `ConfigManager` and nested `EspConfig` / `BoxConfig` / `RadarConfig`). The application reads/writes `config.json` using `ConfigManager.Load()` and `ConfigManager.Save()`.

Notes
-----
- Editing the JSON directly is fine; the loader is case-insensitive and allows comments/trailing commas.
- Color values are ARGB hex strings (AARRGGBB). Examples: `FF8B0000` (opaque dark red), `88FFFFFF` (semi-transparent white).
- Hotkeys use `System.Windows.Forms.Keys` integer values. You can set these by name in code or by numeric VK codes in JSON.

Top-level fields
----------------

- `aimBot` (bool)
  - C# property: `ConfigManager.AimBot`
  - Default: `true`
  - Description: Master switch for AimBot logic (enables/disables the AimBot feature).

- `aimBotAutoShoot` (bool)
  - C# property: `ConfigManager.AimBotAutoShoot`
  - Default: `true`
  - Description: When `true`, the AimBot will automatically trigger mouse clicks when a valid condition is met. When `false`, AimBot only assists aiming; user must fire.

- `bombTimer` (bool)
  - C# property: `ConfigManager.BombTimer`
  - Default: `true`
  - Description: Enables on-screen bomb timer UI used for debugging / local testing.

- `espAimCrosshair` (bool)
  - C# property: `ConfigManager.EspAimCrosshair`
  - Default: `true`
  - Description: Draws a projected crosshair where the AimBot predicts the shot will land.

- `skeletonEsp` (bool)
  - C# property: `ConfigManager.SkeletonEsp`
  - Default: `true`
  - Description: When `true`, draw skeletal bones overlay (joint-to-joint lines).

- `triggerBot` (bool)
  - C# property: `ConfigManager.TriggerBot`
  - Default: `true`
  - Description: Master switch for TriggerBot logic.

- `aimBotKey` (int)
  - C# property: `ConfigManager.AimBotKey` (type: `System.Windows.Forms.Keys`)
  - Default: `Keys.LButton` (left mouse button)
  - JSON representation: integer VK code (e.g., `1`) or name when editing code.
  - Description: Hotkey used for manual-assist AimBot activation.

- `triggerBotKey` (int)
  - C# property: `ConfigManager.TriggerBotKey` (type: `System.Windows.Forms.Keys`)
  - Default: `Keys.LMenu` (left Alt)
  - JSON representation: integer VK code (e.g., `164` for LMenu).

- `teamCheck` (bool)
  - C# property: `ConfigManager.TeamCheck`
  - Default: `true`
  - Description: If true, features (AimBot, TriggerBot, ESP) will ignore teammates.

ESP object (`esp`)
------------------
`esp` is an object with nested `box` and `radar` objects. In C#, this maps to `ConfigManager.Esp` (type `ConfigManager.EspConfig`).

esp.box (BoxConfig)
-------------------
- `enabled` (bool)
  - C#: `ConfigManager.Esp.Box.Enabled`
  - Default: `true`
  - Description: Turn box-style ESP on/off.

- `showName` (bool)
  - C#: `ConfigManager.Esp.Box.ShowName`
  - Default: `true`

- `showHealthBar` (bool)
  - C#: `ConfigManager.Esp.Box.ShowHealthBar`
  - Default: `true`

- `showHealthText` (bool)
  - C#: `ConfigManager.Esp.Box.ShowHealthText`
  - Default: `true`

- `showDistance` (bool)
  - C#: `ConfigManager.Esp.Box.ShowDistance`
  - Default: `true`

- `showWeaponIcon` (bool)
  - C#: `ConfigManager.Esp.Box.ShowWeaponIcon`
  - Default: `true`

- `showArmor` (bool)
  - C#: `ConfigManager.Esp.Box.ShowArmor`
  - Default: `true`

- `showVisibilityIndicator` (bool)
  - C#: `ConfigManager.Esp.Box.ShowVisibilityIndicator`
  - Default: `true`

- `showFlags` (bool)
  - C#: `ConfigManager.Esp.Box.ShowFlags`
  - Default: `true`

- `enemyColor` (string; ARGB hex)
  - C#: `ConfigManager.Esp.Box.EnemyColor`
  - Default: `"FF8B0000"` (opaque dark red)
  - Format: 8 hex chars AARRGGBB. Alpha `FF` is opaque.

- `teamColor` (string; ARGB hex)
  - C#: `ConfigManager.Esp.Box.TeamColor`
  - Default: `"FF00008B"` (opaque dark blue)

- `visibleAlpha` (string; hex)
  - C#: `ConfigManager.Esp.Box.VisibleAlpha`
  - Default: `"FF"`
  - Description: Alpha applied when entity is visible.

- `invisibleAlpha` (string; hex)
  - C#: `ConfigManager.Esp.Box.InvisibleAlpha`
  - Default: `"88"`
  - Description: Alpha applied when entity is not visible.

esp.radar (RadarConfig)
-----------------------
- `enabled` (bool)
  - C#: `ConfigManager.Esp.Radar.Enabled`
  - Default: `true`

- `size` (int)
  - C#: `ConfigManager.Esp.Radar.Size`
  - Default: `150`
  - Allowed: positive integers; typical range `50..600` depending on screen size.

- `x`, `y` (int)
  - C#: `ConfigManager.Esp.Radar.X`, `ConfigManager.Esp.Radar.Y`
  - Default: `50`, `50`
  - Description: Pixel offsets (top-left origin) of radar on the game window.

- `maxDistance` (float)
  - C#: `ConfigManager.Esp.Radar.MaxDistance`
  - Default: `100.0`
  - Allowed: positive float; set to desired world distance (meters) to show blips.

- `showLocalPlayer` (bool)
  - C#: `ConfigManager.Esp.Radar.ShowLocalPlayer`
  - Default: `true`

- `showDirectionArrow` (bool)
  - C#: `ConfigManager.Esp.Radar.ShowDirectionArrow`
  - Default: `true`

- `enemyColor` / `teamColor` (string; ARGB hex)
  - C#: `ConfigManager.Esp.Radar.EnemyColor`, `ConfigManager.Esp.Radar.TeamColor`
  - Default: `"FFFF0000"` (red) / `"FF0000FF"` (blue)

- `visibleAlpha` / `invisibleAlpha` (string; hex)
  - C#: `ConfigManager.Esp.Radar.VisibleAlpha`, `ConfigManager.Esp.Radar.InvisibleAlpha`
  - Default: `"FF"`, `"88"`

How defaults are produced
------------------------
Defaults are set in the C# class initializers and the `ConfigManager.Default()` method in `Utils/ConfigManager.cs`.

Loader behavior
---------------
- `ConfigManager.Load()` does the following:
  - If `config.json` doesn't exist: calls `Default()` and writes a file using `Save()`.
  - Reads `config.json` and deserializes with case-insensitive property names, allows comments and trailing commas.
  - If deserialization returns `null` or nested objects are missing, the loader fills them with defaults (`options.Esp ??= new EspConfig();` etc.).
  - On any exception during load, the loader falls back to `Default()` and writes the default config to disk.

Editing tips
------------
- Use a JSON-aware editor. The serializer uses camelCase when saving; the loader is case-insensitive so `aimBot` or `AimBot` are both accepted.
- To change hotkeys, either put the VK integer (e.g., `1` for left mouse button) in JSON or edit `ConfigManager.Default()` to use a different `Keys` enum member.
- Color preview: many editors/plugins show hex color previews for `#RRGGBB` but not `AARRGGBB`. If you need opacity, keep the `AA` prefix.

Example `config.json` snippet
----------------------------
```json
{
  "aimBot": true,
  "aimBotAutoShoot": true,
  "esp": {
    "box": {
      "enabled": true,
      "enemyColor": "FF8B0000",
      "teamColor": "FF00008B"
    },
    "radar": {
      "enabled": true,
      "size": 150,
      "x": 50,
      "y": 50
    }
  }
}
```

Code references
---------------
- Config definitions and defaults: `Utils/ConfigManager.cs`
- Loading / saving logic: `Utils/ConfigManager.cs` -> `Load()`, `Save()`
- AimBot internals: `Core/AimTrainer.cs`, `Core/NeuralAimNetwork.cs`, `Features/AimBot.cs`
- ESP rendering: `Features/EspBox.cs`, `Features/SkeletonEsp.cs`, `Graphics/ModernGraphics.cs`

FAQ / Troubleshooting
---------------------
- Q: My changes to `config.json` are ignored. A: Ensure the JSON is valid (no trailing commas when your editor doesn't support them) and that the file has proper permissions. The loader overwrites invalid configs with defaults on error.
- Q: I want `skeletonEsp` off by default. A: Edit `ConfigManager.Default()` in `Utils/ConfigManager.cs` and change `SkeletonEsp = true` to `false`, then run to regenerate `config.json` or edit the file directly.

Change log / Notes
------------------
- Keep `Data/Offsets/*` updated from the `CS2-OFFSETS` repository after game updates. Most config fields won't help if offsets are stale.

If you'd like, I can also generate a machine-readable JSON Schema (`config.schema.json`) and add editor integrations (VSCode `settings.json` snippet) so users get autocomplete and validation when editing `config.json`.
