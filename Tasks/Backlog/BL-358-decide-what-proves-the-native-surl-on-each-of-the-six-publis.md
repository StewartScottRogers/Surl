---
id: BL-358
title: Decide what proves the native surl on each of the six published runtime identifiers
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-358 — Decide what proves the native surl on each of the six published runtime identifiers

## Goal

An ADR decides what CI runs against the native-AOT `surl` that `release.yml` publishes, for each of
`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`. Success criterion 4
and Phase 6's "native publish on every platform" are then proven by checks instead of assumed.

## Context

- `Documentation/Product/Product-Overview.md`: success criterion 4 ("A published `surl` runs with no
  .NET runtime installed"), "Constraints" ("Publishing: native ahead-of-time, one file" and
  "Platforms: Windows, Linux and macOS, on x64 and Arm64"), and Phasing row 6. Requirement NFR-006 in
  `Documentation/Product/Requirements.md`.
- What exists on 2026-11-04:
  - `.github/workflows/release.yml` publishes all six runtime identifiers, each on a runner of its own
    OS: `windows-latest`, `windows-11-arm`, `ubuntu-22.04`, `ubuntu-22.04-arm`, and `macos-latest` for
    both macOS identifiers. It runs as a dry run on pull requests that touch `Surl.Console/**`,
    `Directory.Build.props` or `global.json`. Those dry runs passed, most recently on 2026-10-01.
  - Its smoke test only runs `surl --version` and accepts exit 0 or 2, under a stale comment ("until
    Phase 1 lands it exits 2"). The smoke test is skipped for `osx-x64`, because `macos-latest` is
    Arm64.
  - No release has been tagged.
- `Surl.Conformance.UnitTests` runs surl in-process: `SurlOnLoopback` calls the `Surl.Console` entry
  point. So no test has yet run the published binary against upstream curl. `ci.yml` runs those tests
  on `windows-latest`, `ubuntu-latest` and `macos-latest`, each with its pinned reference build.
- Pinned upstream curl exists only for `win-x64`, `linux-x64` and `osx-arm64`. BL-357 asks Stewart
  about `linux-arm64`, `win-arm64` and `osx-x64`.
- `Directory.Build.props` sets `IsAotCompatible` on every production project, and its
  `VerifyAotCompatibility` target fails any project that turns it off.

Decide and record:

1. The smoke test every runtime identifier runs, beyond `--version` (for example: surl serves a listen
   URL and answers one request), and how it shows that no .NET runtime is used (for example: a
   container with no .NET in it, or `DOTNET_ROOT` unset).
2. Whether `Surl.Conformance` runs against the published binary, on which runtime identifiers, and
   how `SurlOnLoopback` is pointed at the binary. BL-359 builds this shape; the planning assumption is
   an environment variable that names the binary.
3. Which runner builds and tests `osx-x64`.
4. What runs on `linux-arm64`, `win-arm64` and `osx-x64` if BL-357 is declined, and what is added if
   it is approved.
5. Which checks run on every pull request (`ci.yml`) and which run only in `release.yml`'s dry run.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`. It is marked
      "Decided by Claude under Stewart's delegation", answers decisions 1 to 5, and is listed in that
      folder's `README.md`.
- [ ] NFR-006 in `Documentation/Product/Requirements.md` names the checks the ADR decides.

## Notes

## Log

- 2026-11-04: Created.
