---
id: BL-1532
title: End an HTTP/1.x, RTSP and WebSocket command-stage delay when the client leaves
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1529, BL-1530]
touches: [Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary, Protocols/Http/Surl.Protocol.Rtsp.UnitTests, Protocols/Http/Surl.Protocol.Ws.UnitLibrary, Protocols/Http/Surl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1532 — End an HTTP/1.x, RTSP and WebSocket command-stage delay when the client leaves

## Goal

An HTTP/1.x, RTSP or WebSocket command-stage `delay` hold ends when the client closes or resets, freeing its slot.

## Context

`Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpCommandFaults.cs:38` calls the overload without a connection. For a connection one exchange owns (HTTP/1.x, RTSP, WebSocket) it passes the connection. HTTP/2 and HTTP/3 keep the connection-less overload (Amendment 2 decision 5). Read BL-1520's amendment to `Documentation/Planning/Decisions/ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md` (Amendment 2) first: it is the specification. Stand-in: no store; a test sees the early end by the `ExchangeEndedEvent` and by a one-connection limit freeing its slot.

## Acceptance criteria

- [x] `HttpCommandFaults` hands the exchange's connection to the helper for HTTP/1.x, RTSP and WebSocket requests, and not for HTTP/2 or HTTP/3 streams.
- [x] A test per family shows the hold ends at the client's half-close without the clock advancing and the client that stays is answered after the delay.
- [x] Coverage 100% of the changed files; build clean with warnings as errors.

## Notes

- `HttpCommandFaults.FindAndApplyAsync` takes an optional `IConnection? connection`; given one it uses `CommandStageFaults`' connection overload (which also aborts on a drop, as the callers did already). The helper cannot tell the family itself, so each server passes its own connection: the Ws and Rtsp responders always, `HttpRequestAnswerer` only when built with a `holdConnection`.
- `touches` widened to the Http, Rtsp and Ws libraries and their tests: no task in Doing names them, and the callers live there. `HttpProtocolServer` builds two answerers: the HTTP/1.x one holds the connection, the one for HTTP/2 streams (prior knowledge and the upgraded h2c streams, via `Http2CleartextUpgradeHandler`'s new `streamAnswerer`) holds none. HTTP/3 already builds its own without one.
- Existing delay tests now pass `clientStays` (the in-memory client half-closes by default, which ends the hold at once).
- Choice: the extended servers that call the helper (S3, Azure, GCS, OCI) keep the connection-less call; not in this task's scope.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. HTTP/1.x, RTSP and WebSocket command-stage delays end when the client half-closes; HTTP/2 and HTTP/3 keep the full hold
