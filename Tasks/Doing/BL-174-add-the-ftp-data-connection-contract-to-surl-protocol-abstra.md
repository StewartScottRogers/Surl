---
id: BL-174
title: Add the FTP data-connection contract to Surl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-173]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: FR-036
created: 2026-09-29
completed:
---
# BL-174 — Add the FTP data-connection contract to Surl.Protocol.Abstractions

## Goal

`Surl.Protocol.Abstractions` holds the data-connection contract BL-173's ADR decides, with a
default that refuses every data connection and an in-memory fake for protocol tests, so the FTP
server (BL-178), `Surl.Networking` (BL-175) and `Surl.Core` (BL-176) can each be built against
it in parallel.

## Context

- Decision: BL-173's ADR, its data-connection seam section (the C# it gives).
- Pattern: ADR-0004 (the listener seam; `InMemoryConnection` lives beside the contract it fakes
  so every protocol test project can use it), ADR-0010 (the in-memory connection's TLS upgrade
  simulation). Abstractions references nothing; every type is in the shared framework.
- The ADR shapes the contract so no existing type with implementers outside this task's touches
  gains a member. `ExchangeContext` keeps its positional constructor: every existing call site
  (`Surl.Core.UnitLibrary/ServingEngine.cs` and the tests) compiles unchanged. If that turns out
  not to hold, stop, move this task to Blocked and have `task-planner` re-scope it.
- The in-memory fake lets a test script what curl does on the data connection (bytes sent,
  bytes expected, an early close, a TLS upgrade) and record which address a passive or active
  request named.

## Acceptance criteria

- [ ] The types BL-173's ADR gives exist with XML docs, including the refusing default and the
      in-memory fake.
- [ ] Tests in `Surl.Protocol.Abstractions.UnitTests` cover every member of the contract, the
      default's refusal, and the fake's scripted accept, connect, early close and upgrade.
- [ ] `ProtocolIsolationTests` pass; `dotnet build -warnaserror` of the whole solution is clean;
      the fast tests are green; `Measure-CodeQuality.ps1 -Library
      Surl.Protocol.Abstractions.UnitLibrary` reports 100% line and branch coverage and no
      failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
