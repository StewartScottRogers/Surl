---
id: BL-843
title: Hold a Surl.Quic flow open through the closing and draining states
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-608]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-843 — Hold a Surl.Quic flow open through the closing and draining states

## Goal

`QuicDatagramFlowLoop` keeps receiving a connection's datagrams and acting on its `NextDeadline`
until its closing or draining state ends, so a client whose `CONNECTION_CLOSE` was lost is answered
again, while the application is told the connection ended as soon as it closes.

## Context

- BL-608 gave `QuicServerConnection` the closing and draining states (RFC 9000 section 10.2,
  ADR-0082 amendment 2): `IsClosing`, `IsDraining`, and `NextDeadline` at their end. `IsClosed`
  still turns true when the connection closes, and `QuicDatagramFlowLoop.RunAsync` returns on it,
  so in a served flow the states are never held.
- Start at `RunAsync` in `Protocols/Http/Surl.Quic.UnitLibrary/QuicDatagramFlowLoop.cs` and
  `QuicApplicationConnection.ObserveAsync`. The application's side should end (`EndAsync`, `Ended`
  cancelled) when the connection closes, not when the closing state ends. Decide what server stop
  (the `ServeAsync` cancellation token) does during the closing state - likely end at once - and
  record it in ADR-0082.
- Check the HTTP/3 server's shutdown tests in `Surl.Protocol.Http` (or `Surl.Http3`) do not now
  wait three probe timeouts on a real clock.

## Acceptance criteria

- [ ] A named `QuicDatagramFlowServerTests` test with `UnitTestTimeProvider`: after the server closes,
      a datagram the client sends is answered with the `CONNECTION_CLOSE` again, and `ServeAsync`
      completes once the clock passes the closing state's end.
- [ ] A named test: the application's `Ended` token is cancelled when the connection closes, before
      the closing state ends.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
