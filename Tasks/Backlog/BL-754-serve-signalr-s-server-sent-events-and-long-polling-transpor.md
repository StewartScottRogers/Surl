---
id: BL-754
title: Serve SignalR's Server-Sent Events and Long Polling transports with HTTP Post
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-449]
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Documentation/Conformance/Extended/SignalR.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-754 — Serve SignalR's Server-Sent Events and Long Polling transports with HTTP Post

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

## Log

- 2026-11-05: Created.
