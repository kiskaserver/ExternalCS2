# CS2GameHelper / ExternalCS2

> Important: This project is for research and educational purposes only. Using these tools to gain an unfair advantage in online multiplayer games violates Valve's Terms of Service and will likely result in a permanent account ban.

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue?logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-10.0-blue?logo=c%23)](https://docs.microsoft.com/dotnet/csharp/)
[![TorchSharp GPU](https://img.shields.io/nuget/v/TorchSharp-cuda-windows?label=TorchSharp-cuda-windows&logo=nuget)](https://www.nuget.org/packages/TorchSharp-cuda-windows/0.105.1)
[![TorchSharp CPU](https://img.shields.io/nuget/v/TorchSharp-cpu?label=TorchSharp-cpu&logo=nuget)](https://www.nuget.org/packages/TorchSharp-cpu/0.105.1)
[![SkiaSharp](https://img.shields.io/nuget/v/SkiaSharp?label=SkiaSharp&logo=nuget)](https://www.nuget.org/packages/SkiaSharp/)
[![Silk.NET](https://img.shields.io/nuget/v/Silk.NET.OpenGL?label=Silk.NET&logo=nuget)](https://www.nuget.org/packages/Silk.NET.OpenGL/)
[![System.Text.Json](https://img.shields.io/nuget/v/System.Text.Json?label=System.Text.Json&logo=nuget)](https://www.nuget.org/packages/System.Text.Json/)
[![System.Drawing.Common](https://img.shields.io/nuget/v/System.Drawing.Common?label=System.Drawing.Common&logo=nuget)](https://www.nuget.org/packages/System.Drawing.Common/)
[![xUnit](https://img.shields.io/nuget/v/xunit?label=xUnit&logo=xunit)](https://www.nuget.org/packages/xunit/)
[![Game: CS2](https://img.shields.io/badge/Game-CS2-red?logo=steam)](https://store.steampowered.com/app/730/CounterStrike_2/)
[![Type: External](https://img.shields.io/badge/Type-External-blue)](https://unknowncheats.me/forum/forum-general/123519-external-vs-internal-hack.html)
[![Latest commit](https://img.shields.io/github/last-commit/kiskaserver/ExternalCS2/main.svg)](https://github.com/kiskaserver/ExternalCS2/commits)
[![Stable release](https://img.shields.io/github/v/release/kiskaserver/ExternalCS2?label=stable%20release)](https://github.com/kiskaserver/ExternalCS2/releases)

<details>
<summary>Tech stack & libraries</summary>

- Language / runtime: C# / .NET 8.0 (net8.0-windows)
- Neural network: TorchSharp (CUDA package: `TorchSharp-cuda-windows`; CPU alternative: `TorchSharp-windows`)
- Graphics & rendering: SkiaSharp
- Windowing / OpenGL: Silk.NET (OpenGL, Windowing, Input)
- JSON config: System.Text.Json
- Numerics: System.Numerics.Vectors
- Drawing utilities: System.Drawing.Common
- Testing: xUnit (`xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`)
- Low-level input / WinAPI interop: custom wrappers in `Core/Kernel32.cs` and `Core/User32.cs`
</details>

## Table of Contents

- [Project Overview](#project-overview)
- [Quick Start — Build & Run](#quick-start--build--run)
- [Neural Network Backend: GPU vs CPU](#neural-network-backend-gpu-vs-cpu)
- [Tested Environment](#tested-environment)
- [Repository Structure](#repository-structure)
- [Configuration (config.json)](#configuration-configjson)
  - [Full Reference → CONFIG_REFERENCE.md](CONFIG_REFERENCE.md)
- [AimBot & Training internals](#aimbot--training-internals)
  - [Detailed design → AimBot.md](AimBot.md)
- [Debugging & Common Issues](#debugging--common-issues)
- [Offsets / DTO Auto-update](#offsets)
- [Community Feedback / Reviews](#community-feedback--reviews)
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

This project was **originally forked from `CS2External` by sweeperxz** but has been **extensively rewritten**, with AimBot and ESP modules implemented from the ground up with new architecture and advanced features.

> **Legal & Ethical Notice**
> This software is provided strictly for research, educational, and local testing purposes. The authors do not condone using this software to gain unfair advantages in online multiplayer games.

## Quick Start — Build & Run

### Release binaries

- The repository's Releases page contains a self-contained CPU build (exe) as a downloadable artifact. This is the recommended build for users without a CUDA-capable GPU.

- A GPU (CUDA) build (exe) is available separately — see the Release notes or the GPU build link included in the release description to download the GPU-accelerated exe.

---

> **Note (CPU build):** This build is intended for any CPU.

> **Note (GPU build):** This build is intended for NVIDIA GPUs with CUDA support only.

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

Default (GPU-accelerated) package:

```xml
<PackageReference Include="TorchSharp-cuda-windows" Version="0.105.1" />
```

CPU-only alternative:

```xml
<PackageReference Include="TorchSharp-cpu" Version="0.105.1" />
```

> Note: CPU training/inference is slower. GPU recommended.

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

## Offsets

Offsets used by the application are read at runtime from the `offsets/` folder located in the repository root. The loader implemented in `Utils/Offsets.cs` reads plain JSON files (for example `client_dll.json`) and maps the required fields into the runtime static offset fields used throughout the project.

Note: the runtime does not depend on generated DTO classes such as `ClientDllDTO` or `OffsetsDTO`. If such DTO files exist in the repository they are provided for reference or historical purposes only; the current code prefers the JSON files placed in `offsets/`.

## Configuration (config.json)

<details>
<summary>Short preview of config fields</summary>

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
  "teamCheck": true
}
```

- For full reference with **types, defaults, and descriptions**, see [CONFIG_REFERENCE.md](CONFIG_REFERENCE.md)

</details>

## AimBot & Training internals

<details>
<summary>Short preview of AimBot internals</summary>

- `_aiAggressiveness`: Adaptive smoothing and FOV
- `HumanReactThreshold` & `SuppressMs`: Reactivity to user input
- `AimTrainer`: Distance-based correction
- `NeuralAimNetwork`: TorchSharp model

> Full detailed design and training explanation → [AimBot.md](AimBot.md)

</details>

## Debugging & Common Issues

- Antivirus / Windows Defender may block memory reads. Run in a controlled environment.  
- Administrator privileges required for global hooks or process reads.  
- Outdated offsets: keep DTOs updated from CS2-OFFSETS.  
- High CPU usage with CPU TorchSharp: switch to CUDA for better performance.

## Offsets / DTO Auto-update

DTOs are updated via: https://github.com/sezzyaep/CS2-OFFSETS

## Community Feedback / Reviews

<div style="display: flex; flex-direction: column; gap: 12px;">

> 🧠 **Advanced Neural Network** – *Alex R., Researcher*  
> "Your code is one of the most advanced self-learning AimBots in the CS2 external tools ecosystem: well-designed, thoroughly documented, and perfect for research purposes."

> 🏗️ **Excellent Architecture** – *Samantha L., Game Developer*  
> "The separation of memory reading, visualization, and neural network logic makes the project easy to modify and reuse."

> 📚 **Ideal for Learning** – *Michael T., AI Enthusiast*  
> "This tool is perfect for studying system programming and machine learning in a real-world application — an outstanding learning resource."

> ⚡ **Reliable Implementation** – *Jessica K., Pro Gamer*  
> "Stable performance in a test environment and clear build/run instructions save hours of setup."

> 🔒 **Ethics & Safety** – *Daniel P., CS2 Player & Researcher*  
> "The project clearly emphasizes ethical use — intended for research only, with recommendations not to use in public matches."

> 📝 **Professional Documentation** – *Emily S., Software Engineer*  
> "Configuration and documentation are organized professionally: easy to get started, understand internals, and experiment with the trainer and models."

</div>

## License

MIT License. See `LICENSE`.

## Contributing

Contributions are welcome for **research/educational purposes only**:

- Bug fixes  
- Training stability / CPU improvements  
- Documented, opt-in features  
- Safe CI updates of Offsets DTOs

Avoid adding code for cheating in live public matches.

## Contact / Notes

- Discord: `frcadm`
- GitHub Discussions: [https://github.com/kiskaserver/ExternalCS2/discussions](https://github.com/kiskaserver/ExternalCS2/discussions)
- Issues for bug reports / feature requests: [https://github.com/kiskaserver/ExternalCS2/issues](https://github.com/kiskaserver/ExternalCS2/issues)
- Run the built executable from `Publish/CS2GameHelper/` or `bin/Release/net8.0-windows/`
- Antivirus must be disabled
- Admin rights required for hooks / memory reading  
- Game must be running before attaching tool