---
name: Security issue
about: Report a security vulnerability or sensitive data exposure responsibly
title: "[SECURITY] "
labels: security
assignees: ''
---

If you believe you have found a security vulnerability in this project (e.g., secret leakage, unsafe privilege escalation, binary tampering instructions, or anything that could lead to account or system compromise), please follow responsible disclosure:

1. DO NOT open a public issue with exploit details.
2. Contact the repository owner privately: `frcadm#` on Discord or email if provided in the project.
3. If you cannot reach project maintainers, consider contacting GitHub Security or your platform's vulnerability disclosure program.

Provide:
- A concise summary of the issue
- Steps to reproduce (privately)
- Impact assessment (privately)

Required environment information (when sharing details privately):

- Software version (from the repository or build): e.g. `CS2GameHelper v1.2.3` (include commit/PR tag if applicable)
- Antivirus / Windows Defender disabled: `Yes` / `No`
- OS version and build: e.g. `Windows 11 24H2 (10.0.22621)`
- Ran as Administrator: `Yes` / `No`
- Processor (CPU): e.g. `AMD Ryzen 5 3600` or `Intel Core i7-9700K`
- Graphics (GPU): e.g. `NVIDIA GeForce RTX 3060 Ti (Driver 536.67)`
- GPU memory / VRAM: e.g. `8 GB`
- TorchSharp backend used: `TorchSharp-cuda-windows` or `TorchSharp-cpu`
- Repro steps and any logs (privately). Attach crash dumps, stdout logs or trimmed debug output.

Example (share privately):

```
Software: CS2GameHelper v1.2.3
Antivirus disabled: Yes
OS: Windows 11 24H2 (10.0.22621)
Admin: Yes
Processor: AMD Ryzen 5 3600
GPU: NVIDIA GeForce RTX 3060 Ti (Driver 536.67)
VRAM: 8 GB
TorchSharp: TorchSharp-cuda-windows (CUDA 11.8)
Notes: Reproduced crash when opening game menu. Attached dump/steps.
```

We will respond with mitigation steps and a timeline for fixes.
