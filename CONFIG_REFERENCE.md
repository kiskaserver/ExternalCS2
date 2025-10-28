# CONFIG_REFERENCE.md

> **Purpose**: Concise reference for `config.json`. Lists every config field, type, C# mapping, default, allowed values, and source code usage.  
> All primary config classes are in `Utils/ConfigManager.cs` (`ConfigManager`, nested `EspConfig`, `BoxConfig`, `RadarConfig`, `AimCrosshairConfig`, `HitSoundConfig`, `SpectatorListConfig`). The app uses `ConfigManager.Load()` and `ConfigManager.Save()`.

---

## Notes

- Editing JSON directly is fine; loader is **case-insensitive** and allows comments/trailing commas.
- Colors are **ARGB hex strings** (`AARRGGBB`), e.g., `FF8B0000` (opaque dark red), `88FFFFFF` (semi-transparent white).
- Hotkeys use integer **virtual key codes** (e.g., `1` = left mouse button, `164` = right Alt). Can be edited as numbers in JSON or via `Keys` enum in code.

---

## Top-level fields

| Field | Type | C# Property | Default | Description |
|-------|------|-------------|---------|-------------|
| `aimBot` | bool | `ConfigManager.AimBot` | `true` | Master switch for AimBot logic. |
| `aimBotAutoShoot` | bool | `ConfigManager.AimBotAutoShoot` | `true` | Automatically trigger mouse clicks if valid target. |
| `bombTimer` | bool | `ConfigManager.BombTimer` | `true` | Show bomb timer UI. |
| `skeletonEsp` | bool | `ConfigManager.SkeletonEsp` | `true` | Draw skeletal overlay instead of bounding boxes. |
| `triggerBot` | bool | `ConfigManager.TriggerBot` | `true` | Master switch for TriggerBot logic. |
| `aimBotKey` | int | `ConfigManager.AimBotKey` | `1` (`Keys.LButton`) | Hotkey for manual AimBot activation. |
| `triggerBotKey` | int | `ConfigManager.TriggerBotKey` | `164` (`Keys.LMenu`) | Hotkey for TriggerBot. |
| `teamCheck` | bool | `ConfigManager.TeamCheck` | `true` | Ignore teammates in AimBot/TriggerBot/ESP. |

---

## ESP object (`esp`)
Maps to `ConfigManager.Esp`.

### `esp.box` (BoxConfig)

| Field | Type | C# Property | Default | Description |
|-------|------|-------------|---------|-------------|
| `enabled` | bool | `ConfigManager.Esp.Box.Enabled` | `true` | Enable box-style ESP. |
| `showName` | bool | `ConfigManager.Esp.Box.ShowName` | `true` | Show player name. |
| `showHealthBar` | bool | `ConfigManager.Esp.Box.ShowHealthBar` | `true` | Show health bar. |
| `showHealthText` | bool | `ConfigManager.Esp.Box.ShowHealthText` | `true` | Show health text. |
| `showDistance` | bool | `ConfigManager.Esp.Box.ShowDistance` | `true` | Show distance to player (in meters). |
| `showWeaponIcon` | bool | `ConfigManager.Esp.Box.ShowWeaponIcon` | `true` | Show weapon icon using custom font. |
| `showArmor` | bool | `ConfigManager.Esp.Box.ShowArmor` | `true` | Show armor value and helmet indicator. |
| `showVisibilityIndicator` | bool | `ConfigManager.Esp.Box.ShowVisibilityIndicator` | `true` | Show visible/invisible status. |
| `showFlags` | bool | `ConfigManager.Esp.Box.ShowFlags` | `true` | Show status flags (flashed, scoped, etc.). |
| `enemyColor` | string (ARGB) | `ConfigManager.Esp.Box.EnemyColor` | `"FF8B0000"` | Enemy box color (dark red). |
| `teamColor` | string (ARGB) | `ConfigManager.Esp.Box.TeamColor` | `"FF00008B"` | Team box color (dark blue). |
| `visibleAlpha` | string (hex) | `ConfigManager.Esp.Box.VisibleAlpha` | `"FF"` | Alpha for visible entity (`FF` = opaque). |
| `invisibleAlpha` | string (hex) | `ConfigManager.Esp.Box.InvisibleAlpha` | `"88"` | Alpha for invisible entity (`88` ≈ 53% opacity). |

### `esp.radar` (RadarConfig)

| Field | Type | C# Property | Default | Description |
|-------|------|-------------|---------|-------------|
| `enabled` | bool | `ConfigManager.Esp.Radar.Enabled` | `true` | Enable radar. |
| `size` | int | `ConfigManager.Esp.Radar.Size` | `150` | Radar pixel size. |
| `x`, `y` | int | `ConfigManager.Esp.Radar.X/Y` | `50`, `50` | Pixel offset from top-left corner. |
| `maxDistance` | float | `ConfigManager.Esp.Radar.MaxDistance` | `100.0` | Max world distance (units) to display. |
| `showLocalPlayer` | bool | `ConfigManager.Esp.Radar.ShowLocalPlayer` | `true` | Show local player on radar. |
| `showDirectionArrow` | bool | `ConfigManager.Esp.Radar.ShowDirectionArrow` | `true` | Show direction arrow for players. |
| `enemyColor` | string (ARGB) | `ConfigManager.Esp.Radar.EnemyColor` | `"FFFF0000"` | Enemy radar color (red). |
| `teamColor` | string (ARGB) | `ConfigManager.Esp.Radar.TeamColor` | `"FF0000FF"` | Team radar color (blue). |
| `visibleAlpha` | string (hex) | `ConfigManager.Esp.Radar.VisibleAlpha` | `"FF"` | Alpha when visible. |
| `invisibleAlpha` | string (hex) | `ConfigManager.Esp.Radar.InvisibleAlpha` | `"88"` | Alpha when invisible. |

### `esp.aimCrosshair` (AimCrosshairConfig)

| Field | Type | C# Property | Default | Description |
|-------|------|-------------|---------|-------------|
| `enabled` | bool | `ConfigManager.Esp.AimCrosshair.Enabled` | `true` | Draw dynamic aim crosshair with recoil compensation. |
| `radius` | int | `ConfigManager.Esp.AimCrosshair.Radius` | `6` | Crosshair line length (pixels). |
| `color` | string (ARGB) | `ConfigManager.Esp.AimCrosshair.Color` | `"FFFFFFFF"` | Crosshair color (white, opaque). |
| `recoilScale` | float | `ConfigManager.Esp.AimCrosshair.RecoilScale` | `2.0` | Multiplier applied to aim punch for crosshair offset. |

---

## Spectator List (`spectatorList`)

| Field | Type | C# Property | Default | Description |
|-------|------|-------------|---------|-------------|
| `enabled` | bool | `ConfigManager.SpectatorList.Enabled` | `true` | Show list of spectators watching you. |

---

## Hit Sound & Text (`hitSound`)

| Field | Type | C# Property | Default | Description |
|-------|------|-------------|---------|-------------|
| `enabled` | bool | `ConfigManager.HitSound.Enabled` | `true` | Enable hit sound and on-screen text. |
| `hitColor` | string (ARGB) | `ConfigManager.HitSound.HitColor` | `"FFFFFFFF"` | Color for "HIT" text (white). |
| `headshotColor` | string (ARGB) | `ConfigManager.HitSound.HeadshotColor` | `"FFFFD700"` | Color for "HEADSHOT" text (gold). |
| `hitText` | string | `ConfigManager.HitSound.HitText` | `"HIT"` | Text shown on non-headshot hit. |
| `headshotText` | string | `ConfigManager.HitSound.HeadshotText` | `"HEADSHOT"` | Text shown on headshot. |
| `hitSoundFile` | string | `ConfigManager.HitSound.HitSoundFile` | `"assets/sounds/hit.wav"` | Path to hit sound file (relative to app). |
| `headshotSoundFile` | string | `ConfigManager.HitSound.HeadshotSoundFile` | `"assets/sounds/headshot.wav"` | Path to headshot sound file. |
| `headshotDamageThreshold` | int | `ConfigManager.HitSound.HeadshotDamageThreshold` | `100` | Damage ≥ this value triggers "HEADSHOT". |
| `textDurationSeconds` | double | `ConfigManager.HitSound.TextDurationSeconds` | `1.5` | Duration (seconds) for on-screen hit text. |

---

## Defaults & Loader Behavior

- Defaults set in `ConfigManager.Default()`.
- `Load()`:
  - Creates `config.json` with defaults if missing.
  - Case-insensitive deserialization (`aimBot` ≡ `AimBot`).
  - Fills missing nested objects with defaults.
  - On JSON parse error, falls back to defaults and overwrites file.

## Editing Tips

- Use a JSON editor with schema validation.
- Colors: include `AA` prefix for opacity (e.g., `88FF0000` = semi-transparent red).
- Hotkeys: use **virtual key codes** (e.g., `32` = Space, `87` = W).
- Paths: relative to executable directory (e.g., `assets/sounds/hit.wav`).

## Example `config.json`

```json
{
  "aimBot": true,
  "aimBotAutoShoot": true,
  "bombTimer": true,
  "skeletonEsp": false,
  "triggerBot": true,
  "aimBotKey": 1,
  "triggerBotKey": 164,
  "teamCheck": true,
  "esp": {
    "box": {
      "enabled": true,
      "showName": true,
      "showHealthBar": true,
      "showHealthText": true,
      "showDistance": true,
      "showWeaponIcon": true,
      "showArmor": true,
      "showVisibilityIndicator": true,
      "showFlags": true,
      "enemyColor": "FF8B0000",
      "teamColor": "FF00008B",
      "visibleAlpha": "FF",
      "invisibleAlpha": "88"
    },
    "radar": {
      "enabled": true,
      "size": 150,
      "x": 50,
      "y": 50,
      "maxDistance": 100.0,
      "showLocalPlayer": true,
      "showDirectionArrow": true,
      "enemyColor": "FFFF0000",
      "teamColor": "FF0000FF",
      "visibleAlpha": "FF",
      "invisibleAlpha": "88"
    },
    "aimCrosshair": {
      "enabled": true,
      "radius": 6,
      "color": "FFFFFFFF",
      "recoilScale": 2.0
    }
  },
  "spectatorList": {
    "enabled": true
  },
  "hitSound": {
    "enabled": true,
    "hitColor": "FFFFFFFF",
    "headshotColor": "FFFFD700",
    "hitText": "HIT",
    "headshotText": "HEADSHOT",
    "hitSoundFile": "assets/sounds/hit.wav",
    "headshotSoundFile": "assets/sounds/headshot.wav",
    "headshotDamageThreshold": 100,
    "textDurationSeconds": 1.5
  }
}
```

## Code References

- **Definitions**: `Utils/ConfigManager.cs`
- **Loading/Saving**: `ConfigManager.Load()`, `ConfigManager.Save()`
- **AimBot**: `Features/AimBot.cs`
- **ESP**: `Features/EspBox.cs`, `Features/EspAimCrosshair.cs`
- **HitSound**: `Features/HitSound.cs`
- **Radar**: `Features/Radar.cs`
- **SpectatorList**: `Features/SpectatorList.cs`

## FAQ / Troubleshooting

- **Changes ignored?** → Check JSON syntax; invalid JSON is replaced with defaults.
- **Sound not playing?** → Verify `assets/sounds/` exists and files are `.wav` PCM.
- **ESP not showing?** → Ensure game is in **Borderless Windowed** mode.
- **Offsets outdated?** → Update from `sezzyaep/CS2-OFFSETS` or use local cache.
