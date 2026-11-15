---
id: BL-1182
title: Apply command-stage delay and drop fault rules in the extended WebTransport server
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: FR-062
created: 2026-11-14
completed: 2026-11-14
---
# BL-1182 — Apply command-stage delay and drop fault rules in the extended WebTransport server

## Goal

A `delay;stage=command` or `drop;stage=command` fault rule matching a WebTransport session's extended CONNECT makes the server hold or cut off that session, instead of ignoring it.

## Context

- Stand-in audit 2026-11-14: `Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary/WebTransportConnectFaults.cs` asks `context.Faults.FindAsync` at the extended CONNECT (called from `WebTransportConnectHandler.cs`) and applies only `error` (BL-1065). Call `CommandStageFaults.FindAndApplyAsync` (`Surl.Protocol.Abstractions`, ADR-0141) at the same point so delay and drop apply and `error` is handed back.
- WebTransport runs over HTTP/3 on QUIC: a drop here aborts the request stream's connection as `IConnection.Abort()` does for the exchange, and a delay holds the CONNECT before its response. Note in the task's log line what the helper's `connection` argument is here (the QUIC connection or stream the handler has). Judged by the WebTransport over HTTP/3 draft and RFC 9220 (ADR-0083).

## Acceptance criteria

- [x] Named tests: a `delay` rule at the command stage holds the extended CONNECT for `ms` on a fake `TimeProvider`, pausing and restarting the idle clock, then answers `200` as usual; one `FaultAppliedEvent` (`Delay`, `Command`).
- [x] Named tests: a `drop` rule aborts with no response sent; one `FaultAppliedEvent` (`Drop`, `Command`).
- [x] Every existing chosen-error test passes unchanged, and a rule for another path changes nothing.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

- The helper's connection argument: the 3-argument overload (no connection) is used, as ADR-0141 amendment 1 says for HTTP/3; a drop resets the CONNECT request stream with H3_INTERNAL_ERROR rather than aborting the QUIC connection other streams share. Idle-clock pause/restart is the helper's, tested in its own library.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. WebTransport CONNECT applies command-stage delay and drop fault rules
