---
id: BL-757
title: Serve SignalR's Server-Sent Events and Long Polling transports with HTTP Post
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-449, BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Documentation/Conformance/Extended/SignalR.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-757 — Serve SignalR's Server-Sent Events and Long Polling transports with HTTP Post

## Goal

The SignalR server serves the Server-Sent Events and Long Polling transports, each paired with HTTP Post for the
client's half, and negotiate lists them in `availableTransports`.

## Context

- Decision: ADR-0119 decision 2 (filed by BL-447). Specification: dotnet/aspnetcore `main` commit
  `6aacb16a27a677d16278a9814ed04f871020854b`, `src/SignalR/docs/specs/TransportProtocols.md`, sections "HTTP Post",
  "Server-Sent Events" and "Long Polling" (with its "Version 1" and "Version 0" `id` rules).
- Builds on BL-449's connection model (tokens, `404`, `409`, the hub's records). Through `Surl.HttpServing`'s
  `IHttpRequestHandler`; an SSE response stays open, so it needs a streaming answer from `Surl.HttpServing`. If that
  library cannot stream a response body today, add the task's touches as the board's rule 3 says.
- Matrix rows `SignalR:TP-ServerSentEvents-1`, `SignalR:TP-LongPolling-1` and `SignalR:TP-HttpPost-1` in
  `Documentation/Conformance/Extended/SignalR.md` become rows of their own naming tests.

## Acceptance criteria

- [ ] Negotiate's `availableTransports` lists WebSockets, `{"transport":"ServerSentEvents","transferFormats":["Text"]}`
      and `{"transport":"LongPolling","transferFormats":["Text","Binary"]}`, pinned by a named test.
- [ ] Named tests show, from TransportProtocols.md: SSE `GET` with `Accept: text/event-stream` answered
      `Content-Type: text/event-stream` and each record as an event's `data`; `400` without `id`, `404` for an unknown
      one; a Long Polling `GET` answered with the waiting records, `200` with no content on the server's poll timeout,
      `204` for a superseded poll; HTTP Post `200` once processed, `409` for a second outstanding post, `404` for an
      ended connection; `DELETE` ending the connection and completing the poll with `204`.
- [ ] The matrix rows are replaced by rows naming those tests; `dotnet build -warnaserror` is clean, the fast tests
      are green, and the library keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 2): Stopped before the first build: `dotnet build` finds no SDK matching `global.json`'s
  10.0.401 (only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed), so the task waits on BL-1035.
  Started code is left uncommitted for the shift's stash. Design chosen, to carry on with:
  - A transport seam: `ISignalRHubTransport` (`ReadPayloadAsync`, `SendRecordAsync`, `WriteEndAsync`) and
    `SignalRTransportRead`; `SignalRHubSession` keeps the hub logic (records, clock, streams) and
    `SignalRWebSocketTransport` takes the frame reading, Pong, write gate and WebSocket Close out of it.
    Asking `ReadPayloadAsync` again tells an HTTP Post transport its payload was processed (`200`).
  - Server-Sent Events: `GET [endpoint-base]?id=` with `Accept: text/event-stream` takes the connection up and
    answers through an `IHttpConnectionTakeover` (no `Surl.HttpServing` change needed): `200`, `Content-Type:
    text/event-stream`, `Cache-Control: no-cache`, `Connection: close`, then each record as `data: <line>\r\n`
    per line and a blank line; the client closing the stream ends the session silently.
  - Long Polling: a non-SSE `GET` with `id` takes the connection up and the first poll answers `200` empty at
    once (as ASP.NET Core does); the session then runs on the first poll's `ShutdownToken` only. Later polls
    answer the queued records (`application/octet-stream`), `200` empty after a 90 s poll timeout, `204` to a
    superseded poll; `DELETE` answers `202` and completes the poll with `204`. Pings and the 30 s client
    timeout apply to every transport, which also forgets abandoned Long Polling connections.
  - HTTP Post: `POST [endpoint-base]?id=` reads the body (`--max-filesize` via the body reader, `--max-message`
    per record via `HubRecordReader`), `200` once processed, `409` for a second outstanding post, `404` unknown
    or ended; `400` without `id` on any non-upgrade request, `400` for a transport mismatch; other methods `405
    Allow: GET, POST, DELETE`. Record these as an amendment to ADR-0119 decision 2 (add the ADR to touches).

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-11: Backlog -> Doing.
