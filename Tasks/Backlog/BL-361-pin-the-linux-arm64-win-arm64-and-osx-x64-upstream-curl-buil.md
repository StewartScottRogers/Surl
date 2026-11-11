---
id: BL-361
title: Pin the linux-arm64, win-arm64 and osx-x64 upstream curl builds and run the published surl against them
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-357, BL-360, BL-1035]
touches: [UpstreamCurlBuilds.json, Documentation/Planning/Decisions, .github/workflows/ci.yml, .github/workflows/release.yml, Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-361 — Pin the linux-arm64, win-arm64 and osx-x64 upstream curl builds and run the published surl against them

## Goal

`UpstreamCurlBuilds.json` pins stunnel/static-curl's 8.21.0 builds for `linux-arm64`, `win-arm64` and
`osx-x64`. CI obtains and verifies them, and `Surl.Conformance` runs against the published `surl` on
those three runtime identifiers using them.

## Context

- Start only if BL-357's Log records Stewart's approval. If he declined, this task is in `Deferred`.
- Assets, from https://github.com/stunnel/static-curl/releases/tag/8.21.0:
  `curl-linux-aarch64-musl-8.21.0.tar.xz`, `curl-windows-aarch64-8.21.0.tar.xz` and
  `curl-macos-x86_64-8.21.0.tar.xz`.
- Shape to copy: ADR-0016 (how CI downloads a stunnel build, caches it by SHA-256 and verifies it),
  and the steps of `.github/workflows/ci.yml` from "Read the upstream curl pin" to "Verify the
  upstream curl build".
- `UpstreamCurlLocator.CurrentPlatform` (`Conformance/Surl.Conformance.UnitLibrary/UpstreamCurlLocator.cs`) builds
  the platform name from the OS and architecture. Check that it answers `linux-arm64`, `win-arm64`
  and `osx-x64` on those runners.
- Decision 4 of BL-358's ADR says where these runs go. BL-360 wired up the other three runtime
  identifiers.

## Acceptance criteria

- [ ] `UpstreamCurlBuilds.json` has three new entries. Each has `"role": "reference"`, a
      `defaultPath`, the SHA-256 of the extracted `curl` binary, and the `version`, `protocols` and
      `features` lines from running that binary. Each `pinned` date is the day it was measured.
- [ ] An ADR under `Documentation/Planning/Decisions` records the three pins and the reason for them
      (BL-357's approval), and is listed in that folder's `README.md`.
- [ ] CI downloads, caches and verifies each build on its runner, and runs `Surl.Conformance` against
      the published binary there. Notes record the URL of a green run.
- [ ] `UpstreamCurlBuildPinsTests` and `UpstreamCurlLocatorTests` pass with the new entries.

## Notes

- 2026-11-11 (lane 4): not started - `dotnet` cannot resolve the SDK `global.json` pins (10.0.401; installed are
  10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100, and `rollForward: latestFeature` does not cross a major version), so
  `UpstreamCurlBuildPinsTests` and `UpstreamCurlLocatorTests` cannot be run. BL-357's approval is on record, so
  the downloads themselves are cleared. The task waits on BL-1035. Nothing was changed, so nothing is left to stash.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing can be built or tested
