---
id: BL-692
title: Say in Requirements.md that SshHostKeyComposition lives in Surl.Composition
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-613]
touches: [Documentation/Product/Requirements.md]
requirement: FR-039
created: 2026-11-04
completed:
---
# BL-692 — Say in Requirements.md that SshHostKeyComposition lives in Surl.Composition

## Goal

`Documentation/Product/Requirements.md` names the project `SshHostKeyComposition` lives in as it is now,
`Surl.Composition`.

## Context

- BL-613 moved `SshHostKeyComposition` (and the other start compositions) from `Application/Surl.Console` to
  `Application/Surl.Composition.UnitLibrary` (ADR-0099 decision 1).
- FR-039's row in `Documentation/Product/Requirements.md` still says "`SshHostKeyComposition` in `Surl.Console`".
  BL-613 could not change it because BL-642, in `Doing` at the time, touched `Requirements.md`.

## Acceptance criteria

- [ ] `Documentation/Product/Requirements.md` says `SshHostKeyComposition` in `Surl.Composition`, and no line in it
      places `AuthenticationComposition`, `KeytabComposition`, `SshHostKeyComposition`, `SshAlgorithmComposition`,
      `ServerTlsComposition`, `DataDirectoryProbe` or `DataDirectoryLock` in `Surl.Console`.

## Notes

## Log

- 2026-11-04: Created.
