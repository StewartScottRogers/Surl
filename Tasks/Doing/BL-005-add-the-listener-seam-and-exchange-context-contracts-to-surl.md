---
id: BL-005
title: Add the listener seam and exchange-context contracts to Surl.Protocol.Abstractions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-000]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-005 — Add the listener seam and exchange-context contracts to Surl.Protocol.Abstractions

## Goal

`Surl.Protocol.Abstractions.UnitLibrary` contains every type the listener-seam ADR
(recorded by BL-000) names: the connection, datagram flow, protocol server, exchange
context and listen-URL types, and the in-memory connection that protocol tests use to
replay byte scripts. Each type has the ADR's name and members.

## Context

- The ADR recorded by BL-000 under `Documentation/Planning/Decisions/` is the
  specification. Its `README.md` index names it. Where the ADR and this task disagree,
  the ADR wins.
- `Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md`: references nothing, never
  constructs a `Socket`, `TcpListener`, `UdpClient` or `SslStream`.
- Coverage gate: 100% line and branch for every production member (root `CLAUDE.md`,
  "Quality gates"). Interfaces carry no lines. Records, the listen-URL type and the
  in-memory connection do, and need tests.
- `.claude/rules/csharp-style.md`: one public type per file, XML docs on every public
  member, records for immutable data.
- The TLS contract is not part of this task. BL-006 adds it after BL-002 decides it.

## Acceptance criteria

- [ ] Each type the ADR names exists in `Surl.Protocol.Abstractions.UnitLibrary`, one
      public type per file, with the ADR's name and members and an XML doc comment on
      every public member.
- [ ] The in-memory connection the ADR places here replays a given sequence of inbound
      byte chunks, records every byte written in order, and reports half-close and abort
      as the ADR defines. `InMemoryConnectionTests` (or the test class named after the
      type the ADR chose) covers each of those, plus reads after the script is exhausted
      and cancellation.
- [ ] The listen-URL type has tests for equality and for the bound-port member the ADR
      defines.
- [ ] `ProtocolIsolationTests.Abstractions_ReferencesNothing` still passes: the project
      has no `ProjectReference` and no `PackageReference`.
- [ ] `dotnet build Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean, and
      `dotnet test --filter "TestCategory!=Integration"` is green with no test in
      `Surl.Protocol.Abstractions.UnitTests` tagged `Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Abstractions.UnitLibrary`.
- [ ] `Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md` no longer says the library
      holds only `SurlExitCode`, and lists the contracts it now holds.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
