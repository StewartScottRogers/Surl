---
id: BL-1211
title: Pass the exchange on the Socket.IO server's namespace-connect logins
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests]
requirement: FR-060
created: 2026-11-15
completed:
---
# BL-1211 — Pass the exchange on the Socket.IO server's namespace-connect logins

## Goal

The Socket.IO server's namespace-connect logins carry their exchange, so each `LoginJudgedEvent` has the exchange ID and a `refuse-login;listener=socketio://...` rule applies.

## Context

- Found by the stand-in audit of 2026-11-15. `Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoConnectCredentials.cs` lines 25-40 build `PasswordLogin(scheme, ..., tlsSession)` without the `Exchange` argument. `SocketIoSession.cs` line 502 calls `SocketIoConnectCredentials.Read(packet.Payload, context.Scheme, tlsSession)` and holds the session's `ExchangeContext` (line 70).
- Effect: `LoginJudgedEvent.ExchangeId` is null and a listener-scoped `refuse-login` rule never matches (`AuthenticationPolicy.IsRefusedByFaultAsync` takes `ListenUrl` from the exchange). BL-1083 made the same change for other servers.
- Note that a Socket.IO session can span several HTTP polling exchanges. Pass the exchange of the session's own `context`, the one that opened it, and say so in the doc comment.

## Acceptance criteria

- [ ] `SocketIoConnectCredentials.Read` takes the `LoginExchange` (or the context), and every `PasswordLogin` it returns carries it.
- [ ] A test in `Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests` shows the policy receives an `Exchange` whose ID and scheme are the session's for a connect with `auth` credentials, and one for a connect without them.
- [ ] `dotnet build Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
