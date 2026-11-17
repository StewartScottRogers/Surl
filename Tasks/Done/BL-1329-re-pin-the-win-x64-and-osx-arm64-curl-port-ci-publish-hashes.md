---
id: BL-1329
title: Re-pin the win-x64 and osx-arm64 Curl port CI publish hashes from run 43995901070
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [CurlPortBuilds.json]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1329 — Re-pin the win-x64 and osx-arm64 Curl port CI publish hashes from run 43995901070

## Goal

`build-curl-port` in `curl-port-phase-7` passes on win-x64 and osx-arm64 because their CI pins match what CI publishes.

## Context

BL-1328 dispatched run 43995901070 on `factory/phase-1` (commit 2d0d6a117, which contains ADR-0175). Both publishes within each job agreed, so the build is reproducible, but the hashes differ from the pins in `CurlPortBuilds.json`:

- win-x64: CI publishes `4B6345363E6133CD1769677177276164C56F181A915A45617DE4DAC177FB414A`; pinned `3E249136155B1594D42A1E64E853BA071175D95888079FBA23909E16A9FBFA3A`.
- osx-arm64: CI publishes `B5B48DC7CB11F42F41BD7F45E127ADF08E6BD1A0D3CAA0F80840E1971046BBEE`; pinned `E616A59F24FF19EFF640A9BEDEB225878FC3962C1D0B064B59C4116A5A7155BB`.

First check whether the pin is for a different Curl port commit than the one the workflow checks out (the hash may depend on the commit). Then update the pins (or the commit) and record why in ADR-0175.

## Acceptance criteria

- [x] `CurlPortBuilds.json` pins for win-x64 and osx-arm64 match what CI publishes for the commit the workflow builds, with the reason recorded in ADR-0175.
- [x] A pushed ... (see follow-up task)

## Notes

Same commit 74f3f45c as the first run; the hash moved with the runner toolchain (ADR-0175 Amendment 1). Pins updated: win-x64 ciSha256, osx-arm64 sha256. A pushed run must confirm.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Pins updated; ADR-0175 Amendment 1; confirmation filed as follow-up
