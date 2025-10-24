# CONFIG_REFERENCE.md

> Purpose: Concise reference for `config.json`. Lists every config field, type, C# mapping, default, allowed values, and source code usage.

All primary config classes are in `Utils/ConfigManager.cs` (`ConfigManager`, nested `EspConfig`, `BoxConfig`, `RadarConfig`). The app uses `ConfigManager.Load()` and `ConfigManager.Save()`.

---

## Notes

- Editing JSON directly is fine; loader is **case-insensitive** and allows comments/trailing commas.
- Colors are ARGB hex strings (`AARRGGBB`), e.g., `FF8B0000` (opaque dark red), `88FFFFFF` (semi-transparent white).
- Hotkeys use `System.Windows.Forms.Keys` integers. Can use enum names in code or numeric VK codes in JSON.

---

## Top-level fields

| Field | Type | C# Property | Default | Description |
|-------|------|-------------|---------|-------------|
| `aimBot` | bool | `ConfigManager.AimBot` | true | Master switch for AimBot logic. |
| `aimBotAutoShoot` | bool | `ConfigManager.AimBotAutoShoot` | true | Automatically trigger mouse clicks if valid target. |
| `bombTimer` | bool | `ConfigManager.BombTimer` | true | Show bomb timer UI for debugging/testing. |
| `espAimCrosshair` | bool | `ConfigManager.EspAimCrosshair` | true | Draw predicted AimBot crosshair. |
| `skeletonEsp` | bool | `ConfigManager.SkeletonEsp` | true | Draw skeletal overlay instead of bounding boxes. |
| `triggerBot` | bool | `ConfigManager.TriggerBot` | true | Master switch for TriggerBot logic. |
| `aimBotKey` | int | `ConfigManager.AimBotKey` | `Keys.LButton` (1) | Hotkey for manual AimBot activation. |
| `triggerBotKey` | int | `ConfigManager.TriggerBotKey` | `Keys.LMenu` (164) | Hotkey for TriggerBot. |
| `teamCheck` | bool | `ConfigManager.TeamCheck` | true | Ignore teammates in AimBot/TriggerBot/ESP. |

---

## ESP object (`esp`)
Maps to `ConfigManager.Esp`.

### `esp.box` (BoxConfig)

| Field | Type | C# Property | Default | Description |
|-------|------|-------------|---------|-------------|
| `enabled` | bool | `ConfigManager.Esp.Box.Enabled` | true | Enable box-style ESP. |
| `showName` | bool | `ConfigManager.Esp.Box.ShowName` | true | Show player name. |
| `showHealthBar` | bool | `ConfigManager.Esp.Box.ShowHealthBar` | true | Show health bar. |
| `showHealthText` | bool | `ConfigManager.Esp.Box.ShowHealthText` | true | Show health text. |
| `showDistance` | bool | `ConfigManager.Esp.Box.ShowDistance` | true | Show distance to player. |
| `showWeaponIcon` | bool | `ConfigManager.Esp.Box.ShowWeaponIcon` | true | Show weapon icon. |
| `showArmor` | bool | `ConfigManager.Esp.Box.ShowArmor` | true | Show armor. |
| `showVisibilityIndicator` | bool | `ConfigManager.Esp.Box.ShowVisibilityIndicator` | true | Show visible/invisible indicator. |
| `showFlags` | bool | `ConfigManager.Esp.Box.ShowFlags` | true | Show status flags. |
| `enemyColor` | string (ARGB) | `ConfigManager.Esp.Box.EnemyColor` | `FF8B0000` | Enemy box color. |
| `teamColor` | string (ARGB) | `ConfigManager.Esp.Box.TeamColor` | `FF00008B` | Team box color. |
| `visibleAlpha` | string (hex) | `ConfigManager.Esp.Box.VisibleAlpha` | `FF` | Alpha for visible entity. |
| `invisibleAlpha` | string (hex) | `ConfigManager.Esp.Box.InvisibleAlpha` | `88` | Alpha for invisible entity. |

### `esp.radar` (RadarConfig)

| Field | Type | C# Property | Default | Description |
|-------|------|-------------|---------|-------------|
| `enabled` | bool | `ConfigManager.Esp.Radar.Enabled` | true | Enable radar. |
| `size` | int | `ConfigManager.Esp.Radar.Size` | 150 | Radar pixel size. |
| `x`, `y` | int | `ConfigManager.Esp.Radar.X/Y` | 50, 50 | Pixel offset from top-left. |
| `maxDistance` | float | `ConfigManager.Esp.Radar.MaxDistance` | 100.0 | Max world distance to display. |
| `showLocalPlayer` | bool | `ConfigManager.Esp.Radar.ShowLocalPlayer` | true | Show local player. |
| `showDirectionArrow` | bool | `ConfigManager.Esp.Radar.ShowDirectionArrow` | true | Show direction arrow. |
| `enemyColor` | string (ARGB) | `ConfigManager.Esp.Radar.EnemyColor` | `FFFF0000` | Enemy radar color. |
| `teamColor` | string (ARGB) | `ConfigManager.Esp.Radar.TeamColor` | `FF0000FF` | Team radar color. |
| `visibleAlpha` | string (hex) | `ConfigManager.Esp.Radar.VisibleAlpha` | `FF` | Alpha when visible. |
| `invisibleAlpha` | string (hex) | `ConfigManager.Esp.Radar.InvisibleAlpha` | `88` | Alpha when invisible. |

---

## Defaults & Loader Behavior

- Defaults set in `ConfigManager` initializers and `ConfigManager.Default()`.
- `Load()`:
  - Creates file with defaults if missing.
  - Case-insensitive JSON deserialization; fills missing nested objects with defaults.
  - On exception, falls back to defaults.

## Editing Tips

- JSON editor recommended. Loader is case-insensitive (`aimBot` or `AimBot` accepted).
- Hotkeys: use VK integer in JSON or edit `ConfigManager.Default()`.
- ARGB colors: many editors only preview `#RRGGBB`; include `AA` for opacity.

## Example `config.json`

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

## Code References

- Definitions/defaults: `Utils/ConfigManager.cs`
- Loading/saving: `Utils/ConfigManager.cs` -> `Load()`, `Save()`
- AimBot: `Core/AimTrainer.cs`, `Core/NeuralAimNetwork.cs`, `Features/AimBot.cs`
- ESP rendering: `Features/EspBox.cs`, `Features/SkeletonEsp.cs`, `Graphics/ModernGraphics.cs`

## FAQ / Troubleshooting

- **Changes ignored?** Ensure JSON is valid and permissions allow writing. Invalid JSON will be replaced by defaults.
- **SkeletonEsp off by default?** Edit `ConfigManager.Default()` and set `SkeletonEsp = false`, then regenerate `config.json` or edit directly.

## Change log / Notes

- Keep `Data/Offsets/*` updated from `CS2-OFFSETS` repository. Outdated offsets may break features regardless of config fields.