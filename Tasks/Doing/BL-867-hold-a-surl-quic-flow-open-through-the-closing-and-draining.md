---
id: BL-867
title: Hold a Surl.Quic flow open through the closing and draining states
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-608, BL-1035]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-867 — Hold a Surl.Quic flow open through the closing and draining states

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

- 2026-11-11 (lane 3): `dotnet build` fails before any work - "A compatible .NET SDK was not found"; global.json pins 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. Depends on BL-1035. The finished work is still on branch factory/BL-867-lane-4-20261111-060243 (feat 26bcaf69); resume with git cherry-pick --no-commit of it and fix the post-rebase build break.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 4 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-867-lane-4-20261111-060243; start with git cherry-pick --no-commit factory/BL-867-lane-4-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-11: Backlog -> Doing.
