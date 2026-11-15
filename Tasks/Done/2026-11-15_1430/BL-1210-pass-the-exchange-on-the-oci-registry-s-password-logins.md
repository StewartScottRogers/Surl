---
id: BL-1210
title: Pass the exchange on the OCI registry's password logins
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests]
requirement: FR-060
created: 2026-11-15
completed: 2026-11-15
---
# BL-1210 — Pass the exchange on the OCI registry's password logins

## Goal

The OCI registry's logins carry their exchange, so each `LoginJudgedEvent` has the exchange ID and a `refuse-login;listener=oci://...` rule applies.

## Context

- Found by the stand-in audit of 2026-11-15. `Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary/OciAccessCheck.cs` lines 79 and 83 and `OciTokenEndpoint.cs` lines 98 and 102 build `PasswordLogin(scheme, user, password, tlsSession)` without the fifth `Exchange` argument (`Foundation/Surl.Protocol.Abstractions.UnitLibrary/PasswordLogin.cs`). The scheme is right, but `LoginJudgedEvent.ExchangeId` is null. A listener-scoped rule never matches, because `FaultPoint.ListenUrl` comes from the exchange (`AuthenticationPolicy.IsRefusedByFaultAsync`). `Surl.Api`'s per-exchange login lookup (`ExchangeFacts.HasJudgedLoginFor`) misses these logins.
- `OciAccessCheck` already holds the `ExchangeContext`. Check whether `OciTokenEndpoint` does (its `context` parameter) and pass `LoginExchange.Of(context)`. BL-1083 made the same change for FTP, MQTT, LDAP and Redis.

## Acceptance criteria

- [x] Every `PasswordLogin` the library builds passes `LoginExchange.Of(context)`.
- [x] Tests in `Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests` show the policy (`UnitTestAuthenticationPolicy`) receives an `Exchange` whose ID and scheme are the connection's, for a `/token` login and for an anonymous `/v2/` access check.
- [x] `dotnet build Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary -warnaserror` is clean and the fast tests pass.

## Notes

Both OciAccessCheck and OciTokenEndpoint already held the ExchangeContext; no decision needed. Built with warnings as errors, 380 tests pass.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. OCI password logins now carry LoginExchange.Of(context); tests added
