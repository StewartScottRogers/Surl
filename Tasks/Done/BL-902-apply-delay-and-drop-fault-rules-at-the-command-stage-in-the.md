---
id: BL-902
title: Apply delay and drop fault rules at the command stage in the HTTP, RTSP and WebSocket servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-793, BL-901, BL-1035]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests, Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary, Protocols/Http/Surl.Protocol.Rtsp.UnitTests, Protocols/Http/Surl.Protocol.Ws.UnitLibrary, Protocols/Http/Surl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-12
---
# BL-902 — Apply delay and drop fault rules at the command stage in the HTTP, RTSP and WebSocket servers

## Goal

A `delay` or `drop` fault rule with `stage=command` that matches an HTTP, RTSP or WebSocket request is
applied there - the request waits `ms` on the exchange's `TimeProvider` with the engine's idle clock paused,
or the connection is aborted - instead of being noted as not applied.

## Context

- ADR-0124 decision 4's kind table: `delay` at `Command` is built by "each server at a command"
  (BL-793 to BL-798), `drop` at `Command` by "the serving engine"; ADR-0124 says "the server's own idle and
  head timeouts do not run during" a delay.
- ADR-0134 decision 5 (BL-793): these three servers apply only `Error` at the command stage today;
  `Surl.HttpMessage`'s `HttpCommandFaults.FindChosenErrorAsync` notes any other kind as not applied. A rule
  so found has had its `times` spent by `FaultPlan`, so today a `times=1` command-stage delay is used up
  without effect.
- What is missing: a seam on `ExchangeContext` (or `IConnection`) through which a server pauses the engine's
  idle clock (`ServingEngine`'s `ExchangeDeadlines.PauseIdleClock`/`RestartIdleClock` are engine-private),
  and a way for `HttpRequestAnswerer` (which answers HTTP/1.x, HTTP/2 and HTTP/3) to ask its writer to abort
  instead of answering (`HttpAnswer` has none in `Surl.HttpServing`). For HTTP/2 and HTTP/3 decide whether a
  drop aborts the connection or resets the stream, and record it in an ADR.
- Stand-in: no store change; each application publishes one `FaultAppliedEvent`. Bytes in tests are
  pinned upstream curl 8.21.0's (`Record-CurlExchange.ps1`), never the Curl port's (ADR-0003); record the exit
  code curl gives a dropped request (expected 52 or 56) and a delayed one past `-m`.

## Acceptance criteria

- [x] A named test per server shows a `delay;stage=command;ms=500` rule holds the matching request 500 ms on a
      manual `TimeProvider` and then answers it as without the rule, publishing one `FaultAppliedEvent`.
- [x] A named test per server shows a `drop;stage=command` rule aborts the connection (`InMemoryConnection.Aborted`)
      before any byte of the answer, publishing one `FaultAppliedEvent`.
- [x] A named test in `Surl.Core.UnitTests` shows the idle timeout does not fire during a command-stage delay.
- [x] The ADR decision is recorded (an amendment to ADR-0134 or a new ADR), and `dotnet build -warnaserror`
      is clean, the fast tests are green and every touched library keeps 100% line and branch coverage.

## Notes

Filed by BL-793 (2026-11-05), which applied the `error` kind only.

2026-11-11 (lane 6): `dotnet build` cannot run - global.json pins SDK 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. Added BL-1035 (Stewart reinstalls the SDK) to depends-on and returned the task to Backlog.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-11: Backlog -> Doing.
- 2026-11-12: Doing -> Done. HTTP, RTSP and WebSocket servers apply delay and drop fault rules at the command stage (ADR-0141 amendment 1)
