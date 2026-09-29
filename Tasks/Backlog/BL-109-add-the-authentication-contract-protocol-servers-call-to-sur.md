---
id: BL-109
title: Add the authentication contract protocol servers call to Surl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-100]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
---
# BL-109 — Add the authentication contract protocol servers call to Surl.Protocol.Abstractions

## Goal

`Surl.Protocol.Abstractions` holds the authentication contract ADR-0032 decision 6 names -
the types a protocol server calls to check a login, learn which HTTP challenges to send and
verify an `Authorization` value, with the connection's encryption and any connection-bound
handshake state - so the HTTP and MQTT servers can use it without referencing
`Surl.Authentication`.

## Context

ADR-0032 (BL-100) decision 6 names the types and members; ADR-0002's table (enforced by
`Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`) keeps protocol servers off
`Surl.Authentication`, and `Surl.Authentication.UnitLibrary/CLAUDE.md` says servers receive
what it provides through contracts in Abstractions.

- Abstractions references nothing (`ProtocolIsolationTests.Abstractions_ReferencesNothing`);
  every type used must be in the shared framework.
- Existing contract style to follow: `IConnectionProtocolServer.cs`, `IExchangeLog.cs`,
  `TlsSession.cs` (sealed records), `ExchangeContext.cs`; each public member has an XML doc
  comment citing its ADR.
- If ADR-0032 calls for a test double shipped from Abstractions (as `InMemoryConnection` is
  for connections), add it here with its tests; otherwise protocol test projects write their
  own fakes.
- This task adds types only; no protocol server, `Surl.Authentication` or `Surl.Console`
  code changes (BL-110, BL-114, BL-115, BL-117 do).

## Acceptance criteria

- [ ] Every type and member ADR-0032 decision 6 names exists in
      `Surl.Protocol.Abstractions.UnitLibrary`, namespace `Surl.Protocol.Abstractions`, with
      the names and shapes the ADR gives and XML doc comments citing ADR-0032.
- [ ] `Surl.Protocol.Abstractions.UnitTests` has tests for every member with behaviour
      (records' equality or validation, any test double's recorded calls), and
      `ProtocolIsolationTests` still pass unchanged.
- [ ] `dotnet build -warnaserror` is clean for the whole solution (no implementer of an
      existing interface broke); the fast tests pass;
      `Surl.Protocol.Abstractions.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-09-29: Created.
