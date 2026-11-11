---
id: BL-1037
title: Reinstall the .NET 10.0.4xx SDK that global.json pins
priority: High
assignee: Stewart
pipeline: direct
depends-on: []
touches: [global.json]
requirement: none
created: 2026-11-11
completed:
---
# BL-1037 — Reinstall the .NET 10.0.4xx SDK that global.json pins

## Goal

`dotnet --list-sdks` on the dark factory machine lists a 10.0.4xx SDK again, so `global.json` (10.0.401, `rollForward: latestFeature`) resolves and `dotnet build` runs without a local override.

## Context

- Found by BL-923 on 2026-11-11: `C:\Program Files\dotnet\sdk` holds only `10.0.100-rc.1.25451.107`, `11.0.100-rc.2.26504.105`
  and `11.0.100` (dated 2026-11-10); the 10.0.4xx SDK is gone, likely removed when .NET 11 was installed. Runtime 10.0.15 is
  still there. Every `dotnet` command at the repository root now fails with "A compatible .NET SDK was not found", which
  is plausibly why lanes block tasks with "build failed after rebasing" (BL-859, BL-769).
- Installing an SDK changes the machine, so it is Stewart's. The alternative - moving `global.json` to another SDK band - is a
  toolchain decision that changes CI too; this task asks for the reinstall.

## Acceptance criteria

- [ ] `dotnet --version` at the repository root prints a 10.0.4xx version and `dotnet build` starts compiling.

## Notes

## Log

- 2026-11-11: Created.
