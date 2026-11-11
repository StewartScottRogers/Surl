---
id: BL-886
title: End gRPC calls still open at shutdown with UNAVAILABLE server shutting down
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-443, BL-1035]
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests, Documentation/Conformance/Extended/Grpc.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-886 — End gRPC calls still open at shutdown with UNAVAILABLE server shutting down

## Goal

A gRPC call still open when the server shuts down ends with trailers `grpc-status: 14`,
`grpc-message: server shutting down` (or `RST_STREAM` `CANCEL` when a reply was cut off mid-write), as
ADR-0116 decision 4 says, instead of the stream being cut off with nothing written.

## Context

- ADR-0116 decision 4 ("Shutdown, `--idle-timeout` and `--max-time`") and the `Grpc.md` rows
  `Grpc:HTTP2-GoAway-2` and `Grpc:HTTP2-Errors-1`.
- Today (BL-443) `Http2ConnectionServer` cancels every stream's token and cuts every write off when the
  exchange is cancelled, so `GrpcStreamHandler` cannot write anything once shutdown starts; it treats the
  cancellation as the client leaving and writes nothing.
- `Surl.Http2` needs a way for a handler to finish within the shutdown grace (ADR-0059): e.g. a
  stream-level shutdown signal (`ExchangeContext.ShutdownToken`) that is separate from the token that cuts
  writes off, so the handler can end its call before the farewell `GOAWAY`. Decide the seam in the task's
  plan, without changing how `Surl.Protocol.Http` and `Surl.Extended.Protocol.DnsQuery` behave.

## Acceptance criteria

- [ ] A named `GrpcStreamHandlerTests` test shuts the exchange down while a `Chat` call is open and
      shows trailers `grpc-status: 14`, `grpc-message: server shutting down` on its stream, followed by
      the connection's `GOAWAY` `NO_ERROR`.
- [ ] A named `Http2ConnectionServerTests` test pins the new seam, and the existing `Surl.Http2`,
      `Surl.Protocol.Http` and DNS over HTTPS tests still pass unchanged.
- [ ] `Grpc.md` rows `Grpc:HTTP2-GoAway-2` and `Grpc:HTTP2-Errors-1` name the tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line
      and branch coverage.

## Notes

Filed by BL-443 (dark factory lane 5), which built the gRPC call handling but could not write after
shutdown without changing `Surl.Http2`, outside its `touches`.

2026-11-11 (dark factory lane 5): the code and tests are written but could not be built: `global.json`
pins SDK 10.0.401 and this machine has only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100, so `dotnet build`
fails SDK resolution. Returned to Backlog behind BL-1035, as BL-911 and BL-762 were. The work in place
(left uncommitted for the shift to stash) is:

- The seam, decided here: no change to `IHttp2StreamHandler` or `ExchangeContext`, so `Surl.Protocol.Http`
  and DNS over HTTPS behave as before. `Http2RequestStream` gains `IsExchangeCancelled` (the connection
  token is cancelled: shutdown, `--idle-timeout`, `--max-time`), `WriteFarewellTrailersAsync(fields)`
  and `WriteFarewellResetAsync(code)`, each written within `Http2ConnectionServer.FarewellWriteDeadline`
  on its own deadline token from the exchange's `TimeProvider` (the same way the connection's farewell
  `GOAWAY` is written, ADR-0059 decision 3). `ServeAsync` already awaits every handler before the
  `GOAWAY`, so a handler's farewell lands first. Its constructor takes the `TimeProvider`.
- gRPC: `GrpcStatus.ServerShuttingDown` (14, `server shutting down`); `GrpcResponseWriter.IsWritingReply`
  and `FinishAtShutdownAsync` (`RST_STREAM` `CANCEL` when a reply was cut off mid-write, else Trailers or
  Trailers-Only 14); `GrpcStreamHandler` catches the cancellation `when (stream.IsExchangeCancelled)`
  before the client-`CANCELLED` catch, and logs `ended UNAVAILABLE`.
- Tests: `GrpcStreamHandlerTests.ServeStreamAsync_ShutdownWhileAChatCallIsOpen_EndsItUnavailableServerShuttingDownThenGoAwayNoError`,
  `…_ShutdownBeforeAnyReply_IsTrailersOnlyUnavailable`, `…_ShutdownWhileAReplyWaitsForWindow_ResetsTheStreamCancel`;
  `Http2ConnectionServerTests.ServeAsync_ShutdownWhileAHandlerWaits_LetsItWriteFarewellTrailersBeforeTheGoAway`,
  `…_LetsItWriteAFarewellResetBeforeTheGoAway`, `ServeAsync_ClientClosesWhileAHandlerWaits_IsNotAnExchangeCancellation`.
- Still to do once it builds: run the tests and coverage; add "Amendment 1" to ADR-0116 recording the
  seam (add `Documentation/Planning/Decisions/ADR-0116-how-the-grpc-server-answers-grpc-and-grpcs.md` to
  `touches`; no task in Doing names it); name the tests in `Grpc.md` rows `Grpc:HTTP2-GoAway-2` and
  `Grpc:HTTP2-Errors-1`; update both libraries' `CLAUDE.md` (gRPC's "What it does not do yet" line).

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so the written code and tests cannot be built
