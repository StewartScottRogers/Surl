---
id: BL-085
title: Decide how RTSP conformance runs on macOS, where the pinned osx-arm64 build has no rtsp
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-085 — Decide how RTSP conformance runs on macOS, where the pinned osx-arm64 build has no rtsp

## Goal

An ADR decides how Surl's RTSP server is conformance-tested on macOS, given that the
pinned `osx-arm64` upstream curl 8.21.0 build does not list `rtsp` among its protocols,
and ADR-0016's protocol list is annotated with what the builds actually print.

## Context

- CI run https://github.com/StewartScottRogers/Surl/actions/runs/36537650411 (BL-079)
  printed `--version` for the pinned static-curl builds. `linux-x64` lists `rtsp`;
  `osx-arm64` does not (`... pop3 pop3s scp sftp smb smbs ...`) and adds the
  `AppleSecTrust` feature. `UpstreamCurlBuilds.json` now carries the printed values.
- ADR-0016 quotes the release notes' protocol line, which includes `rtsp` for both.
- Options to weigh: mark macOS RTSP conformance tests
  `[OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]` and rely on Windows and
  Linux; or pin a second macOS build that has `rtsp` (a download, so Stewart's approval).
  Decide by ADR-0003 and CLAUDE.md "Decisions".

## Acceptance criteria

- [x] An ADR under `Documentation/Planning/Decisions` marked "Decided by Claude under
      Stewart's delegation" states how RTSP conformance runs on macOS.
- [x] ADR-0016 notes that the `osx-arm64` build prints no `rtsp` and the `AppleSecTrust`
      feature, citing run 36537650411.

## Notes

- Decided in ADR-0026: an RTSP conformance test is Inconclusive where the current
  platform's pin does not list `rtsp` in its `protocols`, so RTSP conformance runs on
  Windows and Linux; no second macOS build is pinned (no download needed, and RTSP has no
  TLS in curl, so no backend difference to cover). Keyed on the pin's protocol list rather
  than `OSCondition`, so a later pin needs no test edit and the same rule covers any scheme
  a build lacks. The shared check lands with the first RTSP conformance test; no RTSP
  server task exists yet, so none was filed.
- Delivered in the session rather than through `align-and-document`: one new ADR, a note
  in ADR-0016 and an index row, no code.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0026 decides RTSP conformance runs where the pin lists rtsp (Windows, Linux) and is Inconclusive on macOS; ADR-0016 records what the builds print
