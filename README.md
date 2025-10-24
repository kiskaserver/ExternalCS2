# CS2GameHelper / ExternalCS2

> Important: This project is for research and educational purposes only. Using these tools to gain an unfair advantage in online multiplayer games violates Valve's Terms of Service and will likely result in a permanent account ban.

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue?logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-10.0-blue?logo=c%23)](https://docs.microsoft.com/dotnet/csharp/)
[![TorchSharp GPU](https://img.shields.io/nuget/v/TorchSharp-cuda?label=TorchSharp-cuda-windows&logo=nuget)](https://www.nuget.org/packages/TorchSharp-cuda/0.105.1)
[![TorchSharp CPU](https://img.shields.io/nuget/v/TorchSharp-cpu?label=TorchSharp-cpu&logo=nuget)](https://www.nuget.org/packages/TorchSharp-cpu/0.105.1)
[![SkiaSharp](https://img.shields.io/nuget/v/SkiaSharp?label=SkiaSharp&logo=nuget)](https://www.nuget.org/packages/SkiaSharp/)
[![Silk.NET](https://img.shields.io/nuget/v/Silk.NET.OpenGL?label=Silk.NET&logo=nuget)](https://www.nuget.org/packages/Silk.NET.OpenGL/)
[![System.Text.Json](https://img.shields.io/nuget/v/System.Text.Json?label=System.Text.Json&logo=nuget)](https://www.nuget.org/packages/System.Text.Json/)
[![System.Drawing.Common](https://img.shields.io/nuget/v/System.Drawing.Common?label=System.Drawing.Common&logo=nuget)](https://www.nuget.org/packages/System.Drawing.Common/)
[![xUnit](https://img.shields.io/nuget/v/xunit?label=xUnit&logo=xunit)](https://www.nuget.org/packages/xunit/)

## Tech stack & libraries

This project is written in C# targeting .NET 8 (net8.0-windows) and is Windows-focused. Key libraries and frameworks used:

- Language / runtime: C# / .NET 8.0 (net8.0-windows)
- Neural network: TorchSharp (CUDA package: `TorchSharp-cuda-windows`; CPU alternative: `TorchSharp-windows`)
- Graphics & rendering: SkiaSharp
- Windowing / OpenGL: Silk.NET (OpenGL, Windowing, Input)
- JSON config: System.Text.Json
- Numerics: System.Numerics.Vectors
- Drawing utilities: System.Drawing.Common
- Testing: xUnit (`xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`)
- Low-level input / WinAPI interop: custom wrappers in `Core/Kernel32.cs` and `Core/User32.cs`

Most NuGet packages are declared in `CS2GameHelper.csproj`.

## Table of Contents

- [Project Overview](#project-overview)
- [Quick Start — Build & Run](#quick-start--build--run)
- [Neural Network Backend: GPU vs CPU](#neural-network-backend-gpu-vs-cpu)
- [Tested Environment](#tested-environment)
- [Repository Structure](#repository-structure)
- [Configuration (config.json)](#configuration-configjson)
- [AimBot & Training internals](#aimbot--training-internals)
- [Debugging & Common Issues](#debugging--common-issues)
- [Offsets / DTO Auto-update](#offsets--dto-auto-update)
- [License](#license)
- [Contributing](#contributing)
- [Contact / Notes](#contact--notes)

## Project Overview

`ExternalCS2` (distributed as the `CS2GameHelper` assembly) is a Windows desktop application written in .NET 8 (C#). It inspects and interacts with the Counter-Strike 2 (CS2) client process from the outside — without injecting any code into the game itself.

The project contains helper modules for visualization and automation, including:

- Self-learning AimBot
- TriggerBot
- ESP (Extra Sensory Perception)
- Radar
- And more!

It is intended as a research and learning platform for exploring game internals, memory reading, and external tooling.

This project was originally forked from `CS2External` by sweeperxz but has been almost completely rewritten. The AimBot and ESP were implemented from the ground up with new architecture and advanced features.

> **Legal & Ethical Notice**

This software is provided strictly for research, educational, and local testing purposes. The authors do not condone using this software to:

- Gain unfair advantages in online multiplayer games.
- Violate Valve/CS2 EULA or VAC policies.
- Perform actions that could cause account suspension, bans, or legal consequences.

Use responsibly and only in permitted environments.

## Quick Start — Build & Run

### Requirements

- OS: Windows 10/11 (x64)
- .NET SDK: 8.0
- IDE: Visual Studio 2022/2023+ or the `dotnet` CLI

### Commands

```powershell
# Build in Debug mode
dotnet build .\CS2GameHelper.csproj -c Debug

# Run the application
dotnet run --project .\CS2GameHelper.csproj -c Debug

# Publish a self-contained release for Windows x64
dotnet publish .\CS2GameHelper.csproj -c Release -r win-x64 -o .\Publish\CS2GameHelper
```

### Output locations

- `bin/Debug/net8.0-windows/`
- `bin/Release/net8.0-windows/`
- `Publish/CS2GameHelper/`

## Neural Network Backend: GPU vs CPU

The project uses TorchSharp.

Default (GPU-accelerated) package in `CS2GameHelper.csproj`:

```xml
<PackageReference Include="TorchSharp-cuda-windows" Version="0.105.1" />
```

CPU-only alternative:

```xml
<PackageReference Include="TorchSharp-cpu" Version="0.105.1" />
```

Note: CPU training/inference is much slower and more CPU-intensive. Use GPU for best performance.

## Tested Environment

- CPU: AMD Ryzen 5 3600
- GPU: ASUS TUF Gaming GeForce RTX 3060 Ti (8GB)
- RAM: 32GB DDR4 3200MHz
- OS: Windows 11 (24H2)

## Repository Structure

```
ExternalCS2/
├── Core/                   # Low-level utilities (WinAPI, AimTrainer)
├── Data/                   # Data models (players, entities, game state)
├── Features/               # Core modules (AimBot, TriggerBot, ESP, Radar)
├── Graphics/               # Rendering and math utilities
├── Utils/                  # Helper services (config, offsets, hooks)
├── assets/                 # Fonts and other resources
├── CS2GameHelper.csproj    # Project file
├── config.json             # Runtime configuration
└── Program.cs              # Application entry point
```

**Important DTOs**

- `Data/Offsets/ClientDllDTO.cs` — automatically updated from: https://github.com/sezzyaep/CS2-OFFSETS
- `Data/Offsets/OffsetsDTO.cs` — automatically updated from: https://github.com/sezzyaep/CS2-OFFSETS

These files are updated automatically by the project's update script (see `Utils/OffsetsUpdater` or CI steps in your repository, if configured). Keeping these DTOs up to date is critical after game updates.

## Configuration (config.json)

Below is the default configuration file generated on first run. Use this as a template and adjust per your needs.

```json
{
  "aimBot": true,
  "aimBotAutoShoot": true,
  "bombTimer": true,
  "espAimCrosshair": true,
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
    }
  }
}
```

### Global toggles

- `aimBot` (bool): Master switch for AimBot.
- `aimBotAutoShoot` (bool): If true, the AimBot will automatically trigger mouse clicks when a valid shot condition is met.
- `bombTimer` (bool): Enables an on-screen bomb timer UI element.
- `espAimCrosshair` (bool): Draws an auxiliary crosshair where the AimBot predicts the shot will land.
- `skeletonEsp` (bool): When true, draws skeletal overlay instead of or in addition to bounding boxes.
- `triggerBot` (bool): Master switch for TriggerBot.
- `aimBotKey`, `triggerBotKey` (int): Virtual-key codes for hotkeys (Windows VK codes).
- `teamCheck` (bool): If true, filters out teammates.

### `esp.box` settings

- `enabled` (bool): Turn box-style ESP on/off.
- `showName`, `showHealthBar`, `showHealthText`, `showDistance`, `showWeaponIcon`, `showArmor`, `showVisibilityIndicator`, `showFlags` (bools): Visual options.
- `enemyColor`, `teamColor` (string): ARGB hex, e.g. `FF8B0000`.
- `visibleAlpha`, `invisibleAlpha` (string): Hex alpha values.

### `esp.radar` settings

- `enabled` (bool): Toggle radar.
- `size`, `x`, `y` (number): Pixel size and offset.
- `maxDistance` (float): Max world distance shown on radar.
- `showLocalPlayer`, `showDirectionArrow` (bool): Local player indicators.

## AimBot & Training internals

- `HumanReactThreshold` & `SuppressMs`: AimBot monitors raw mouse input and suppresses bot movement for a short window when user input is detected. These are defined in `Core/Humanization`.
- `_aiAggressiveness`: Adaptive parameter derived from recent user movement; influences smoothing and FOV.
- `AimTrainer`: Statistical correction stored per-distance bucket (see `Core/AimTrainer.cs`).
- `NeuralAimNetwork`: TorchSharp model trained online (see `Core/NeuralAimNetwork.cs`).

More detailed design notes and an explanation of how AimBot works are available in `AimBot.md` (field-by-field, algorithm steps, training details).

## Debugging & Common Issues

- Antivirus / Windows Defender: Low-level hooks and external process reads may trigger heuristics. Run in a controlled testing environment or add an exception if trusted.
- Administrator privileges: Some features may require elevated rights.
- Outdated offsets: Keep `ClientDllDTO.cs` and `OffsetsDTO.cs` updated from https://github.com/sezzyaep/CS2-OFFSETS.
- High CPU with CPU TorchSharp: Consider switching to CUDA or reduce training frequency.

## Offsets / DTO Auto-update

DTOs are kept current via the CS2-OFFSETS repository:

Source: https://github.com/sezzyaep/CS2-OFFSETS

If your copy is stale after a CS2 update, run the updater or pull the latest DTOs from that repository.

## License

This project is published under the MIT License.
See the full license in the `LICENSE` file at the project root.

This project is distributed under the MIT license. Refer to `LICENSE` for details.

## Contributing

Contributions are welcome for research and defensive/educational purposes. Please open PRs for:

- Fixing bugs
- Improving training stability / reducing CPU usage
- Adding documented, opt-in features
- CI improvements that safely update Offsets DTOs

Please avoid adding code or instructions that make the project trivially usable for cheating in live public matches.

## Contact / Notes

If you'd like a more compact README and a separate `CONFIG_REFERENCE.md` that documents every `config.json` field with types and code references, tell me which format you prefer and I will generate both files.

Discord: `frcadm`

Running notes
- You can run the built executable directly from the `Publish/CS2GameHelper/` folder or from `bin/Release/net8.0-windows/` — run the `.exe` to start the helper.
- Administrator privileges are required for some features (global hooks, reading other processes). Run the executable as Administrator.
- The game (Counter-Strike 2) must be running and in a playable state for the tool to read game memory; start CS2 before launching the helper or attach while running.
- This tool is external: it reads memory and draws in a separate process/window. By default it is not injected into the game process and is typically not visible to game-capture modes (e.g., OBS Game Capture) or the Discord in-game overlay. If you need to capture it, consider using Display Capture.


