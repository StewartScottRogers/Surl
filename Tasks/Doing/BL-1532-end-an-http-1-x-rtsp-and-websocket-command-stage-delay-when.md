---
id: BL-1532
title: End an HTTP/1.x, RTSP and WebSocket command-stage delay when the client leaves
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1529, BL-1530]
touches: [Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1532 — End an HTTP/1.x, RTSP and WebSocket command-stage delay when the client leaves

## Goal

An HTTP/1.x, RTSP or WebSocket command-stage `delay` hold ends when the client closes or resets, freeing its slot.

## Context

`Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpCommandFaults.cs:38` calls the overload without a connection. For a connection one exchange owns (HTTP/1.x, RTSP, WebSocket) it passes the connection. HTTP/2 and HTTP/3 keep the connection-less overload (Amendment 2 decision 5). Read BL-1520's amendment to `Documentation/Planning/Decisions/ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md` (Amendment 2) first: it is the specification. Stand-in: no store; a test sees the early end by the `ExchangeEndedEvent` and by a one-connection limit freeing its slot.

## Acceptance criteria

- [ ] `HttpCommandFaults` hands the exchange's connection to the helper for HTTP/1.x, RTSP and WebSocket requests, and not for HTTP/2 or HTTP/3 streams.
- [ ] A test per family shows the hold ends at the client's half-close without the clock advancing and the client that stays is answered after the delay.
- [ ] Coverage 100% of the changed files; build clean with warnings as errors.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
