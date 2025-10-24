---
name: Offsets / DTO update
about: Request or document an update to Offsets DTOs (Data/Offsets)
title: "[OFFSETS] "
labels: maintenance
assignees: ''
---

Use this template to request an update of the `Data/Offsets/*` DTOs or to report mismatches after a CS2 update.

**Summary**
- Brief description of which offsets appear broken or what changed in the game.

**Source / reference**
- Link to the upstream offsets commit or repository (e.g. https://github.com/sezzyaep/CS2-OFFSETS)
- Commit hash or tag used

**Files expected to update**
- List of DTO files that should be regenerated/updated (e.g., `ClientDllDTO.cs`, `OffsetsDTO.cs`)

**Steps to reproduce / verify**
- How to validate the offsets (e.g., fields that return 0, observed crashes, incorrect positions)

**Suggested action**
- Regenerate DTOs from upstream and run a local build; optionally include a patch or PR to apply.

**Notes**
- If you provide updated DTOs, attach them or a PR link.
