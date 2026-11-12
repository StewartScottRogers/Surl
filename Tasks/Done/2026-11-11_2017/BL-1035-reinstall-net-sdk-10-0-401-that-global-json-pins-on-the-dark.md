---
id: BL-1035
title: Reinstall .NET SDK 10.0.401 that global.json pins on the dark factory machine
priority: High
assignee: Stewart
pipeline: direct
depends-on: []
touches: [global.json]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-1035 — Reinstall .NET SDK 10.0.401 that global.json pins on the dark factory machine

## Goal

`dotnet build` at the repository root resolves the SDK that `global.json` pins (10.0.401, roll forward
`latestFeature`) on Stewart's machine, so lanes build with the pinned toolchain again.

## Context

- Found by BL-1006's lanes on 2026-11-11: `dotnet build` fails with "A compatible .NET SDK was not found".
  `dotnet --list-sdks` lists only 10.0.100-rc.1.25451.107, 11.0.100-rc.2.26504.105 and 11.0.100 under
  `C:\Program Files\dotnet\sdk`; no 10.0.4xx band is installed. An update (Visual Studio or the .NET 11
  installer) most likely removed it.
- Every lane's build and the shift's integration build fail until this is fixed, whatever the task.
- Installing an SDK is a machine change only Stewart makes. The alternative, re-pinning `global.json`,
  is a toolchain decision that changes every lane and CI; this task asks for the reinstall.

## Acceptance criteria

- [x] `dotnet --list-sdks` lists a 10.0.4xx SDK at or above 10.0.401. (10.0.404, seen 2026-11-11)
- [x] `dotnet build` at the repository root is clean with `global.json` unchanged. (`Application\Surl.Console`: 0 warnings, 0 errors)

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Done. Stewart said close it on 2026-11-11: SDK 10.0.404 is installed, which global.json's latestFeature roll-forward accepts, and dotnet build Application\Surl.Console is clean (0 warnings, 0 errors).
