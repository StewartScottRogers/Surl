---
id: BL-006
title: Add the server-side TLS contract to Surl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-002, BL-005]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-006 — Add the server-side TLS contract to Surl.Protocol.Abstractions

## Goal

`Surl.Protocol.Abstractions.UnitLibrary` contains the TLS types and members the TLS ADR
(recorded by BL-002) names. A protocol server can then receive a secured connection, or
ask for an upgrade, without referencing `SslStream`.

## Context

- The TLS ADR recorded by BL-002 under `Documentation/Planning/Decisions/` is the
  specification. Its `README.md` index names it.
- It extends the connection contract BL-005 added, from the ADR recorded by BL-000.
- Abstractions references nothing and never constructs an `SslStream`
  (`Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md`). Types from
  `System.Security.Cryptography.X509Certificates` and `System.Net.Security` enums such
  as `SslProtocols` are BCL and may appear in signatures.
- The in-memory connection from BL-005 must be able to stand in for a secured connection
  and for an upgrade, so protocol tests can cover `https` and `STARTTLS` paths without
  a network.

## Acceptance criteria

- [ ] Each TLS type or member the ADR names exists with the ADR's name and an XML doc
      comment on every public member.
- [ ] The in-memory connection reports a configurable TLS state and records an upgrade
      request, and tests cover both.
- [ ] `ProtocolIsolationTests.Abstractions_ReferencesNothing` still passes.
- [ ] `dotnet build Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean, and
      `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Abstractions.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
