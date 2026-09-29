---
id: BL-006
title: Add the server-side TLS contract to Surl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-002, BL-005]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Core.UnitLibrary, Surl.Core.UnitTests, Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
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

- 2026-09-28 (lane 1): `touches` widened. ADR-0010 adds `TlsSession` and
  `UpgradeToTlsAsync` to `IConnection`, and ADR-0010 "Consequences" says every existing
  implementation gains both, or its build breaks. Two exist outside Abstractions:
  `Surl.Core.UnitLibrary/RecordingConnection.cs` (the recording decorator: forward both
  to the wrapped connection) and `Surl.Networking.UnitLibrary/StreamConnection.cs`
  (`TlsSession` stays `null`; `UpgradeToTlsAsync` throws until BL-012 implements the
  handshake). Their `.UnitTests` twins are added for the tests covering the new members.
  `Surl.Networking` overlaps BL-055, in Doing, so the task went back to Backlog until
  BL-055 finishes.
- Rejected: default interface members on `IConnection` to avoid touching Core and
  Networking. A decorator that silently keeps a default `TlsSession => null` would hide
  the upgrade from the exchange log, against ADR-0004 section 5 and ADR-0010's
  consequences.
- Plan for the next run: `TlsSession` record, `TlsHandshakeException : IOException`,
  `TlsSchemes.IsImplicitTls`, and `InMemoryConnection` gains `initialTlsSession`,
  `upgradeTlsSession` (default TLS 1.3, `TlsCipherSuite.TLS_AES_128_GCM_SHA256`, no ALPN,
  no client certificate), `upgradeFails`, and `UpgradeRequested`, with the
  `InvalidOperationException` cases ADR-0010 section 1 lists.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Backlog. Needs Surl.Networking.UnitLibrary (StreamConnection implements IConnection), which BL-055 in Doing touches
