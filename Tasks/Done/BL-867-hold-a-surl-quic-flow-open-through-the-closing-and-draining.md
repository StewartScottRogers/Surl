---
id: BL-867
title: Hold a Surl.Quic flow open through the closing and draining states
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-608]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests, Protocols/Http/Surl.Http3.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
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

- [x] A named `QuicDatagramFlowServerTests` test with `UnitTestTimeProvider`: after the server closes,
      a datagram the client sends is answered with the `CONNECTION_CLOSE` again, and `ServeAsync`
      completes once the clock passes the closing state's end.
- [x] A named test: the application's `Ended` token is cancelled when the connection closes, before
      the closing state ends.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- Decision recorded as ADR-0082 amendment 5 (decided by Claude under Stewart's delegation):
  `QuicDatagramFlowLoop.RunAsync` returns once the connection is closed and `NextDeadline` is null;
  the first time it sees the connection closed it ends the application's side (`Ended` cancelled);
  server stop (the `ServeAsync` token) still ends at once, in the closing state too; `Ended` is no
  longer linked to the server's token, so it is cancelled only after the application's side is
  marked ended (the unlinked order fixed a race the change exposed in
  `ServeAsync_CancelledWhileTheApplicationRuns_...`).
- Tests: `ServeAsync_ClientSendsInTheClosingState_AnswersWithTheConnectionCloseAgainAndEndsOnceTheClockPassesTheStatesEnd`,
  `ServeAsync_ClientClosesTheConnection_CancelsEndedBeforeTheDrainingStateEnds` and
  `ServeAsync_CancelledInTheClosingState_ThrowsOperationCanceledAtOnce`. 
  `ServeAsync_OptionsNameDoqAndTheClientOffersOnlyH3_...` now accepts the close repeated, since the
  client's second recorded Initial reaches the closing state.
- Touches widened (rule 3; no task in Doing names them): the shared HTTP/3 test client
  (`Surl.Http3.UnitTests/Http3TestQuicClient.cs`, linked into `Surl.Protocol.Http.UnitTests` and
  the WebTransport tests) awaited `ServeAsync` on a manual clock that never passed the closing
  state, so 4 + 15+ HTTP/3 tests hung 30 s. `FinishAsync` there and in `UnitTestFlowClient` now move
  the clock past the state once the connection has closed (new `IMovableClock`, implemented by both
  `ManualTimeProvider`s and linked in both csproj files); `CloseCode` counts a repeated close once.
  DNS over QUIC tests run on the system clock and stay green without change.
- Toolchain: the pinned SDK 10.0.401 is not installed (BL-1035), so `dotnet build Surl.slnx
  -warnaserror` and the fast tests were run with SDK 11.0.100 from outside the repository (no
  global.json found): 0 warnings, 0 errors, 79 test assemblies all passed; the five QUIC/HTTP/3/DoQ
  suites passed 5 more runs. Coverage of `Surl.Quic.UnitLibrary` measured with the Code Coverage
  collector: line 1, branch 1. `dotnet format --verify-no-changes` could not run (it resolves the SDK
  from the project's folder); files are CRLF as `.editorconfig` asks.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. The QUIC flow loop holds a closed connection through its closing and draining states, answering a lost CONNECTION_CLOSE again, while the application is told at the close
