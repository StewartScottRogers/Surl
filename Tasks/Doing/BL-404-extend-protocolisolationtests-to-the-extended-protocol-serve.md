---
id: BL-404
title: Extend ProtocolIsolationTests to the extended protocol servers
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-543]
touches: [Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-404 — Extend ProtocolIsolationTests to the extended protocol servers

## Goal

`ProtocolIsolationTests` checks every `Surl.Extended.Protocol.*.UnitLibrary`. Each may reference only
Abstractions and ADR-0002's horizontal libraries, must have its `.UnitTests` twin beside it, and no original
`Surl.Protocol.*` server may reference an extended server.

## Context

- Rule: BL-543's ADR (isolation) and ADR-0002 decision 3.
- `Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`: `ProtocolServers()` finds
  `Surl.Protocol.*.UnitLibrary.csproj` only, so an extended server would go unchecked today.
- No extended project exists yet. The tests must pass with none, and fail as soon as a first one breaks a rule.
  The checks are made testable on project names, as `ForbiddenProtocolReferences` is today.

## Acceptance criteria

- [ ] `EveryProtocolServer_ReferencesOnlyAbstractionsAndHorizontalLibraries` and
      `EveryProtocolServer_HasAMatchingTestProjectBesideIt` also cover `Surl.Extended.Protocol.*.UnitLibrary`.
- [ ] A new test, `EveryOriginalProtocolServer_ReferencesNoExtendedServer`, exists. Data-row tests show that
      `Surl.Extended.Protocol.Redis.UnitLibrary` referencing `Surl.Protocol.Http.UnitLibrary` is forbidden, and
      that `Surl.Protocol.Http.UnitLibrary` referencing `Surl.Extended.Protocol.Redis.UnitLibrary` is forbidden.
- [ ] `dotnet test Foundation/Surl.Protocol.Abstractions.UnitTests` passes, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
