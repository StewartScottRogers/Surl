---
id: BL-447
title: Decide how the SignalR server answers signalr and signalrs
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/SignalR.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-447 — Decide how the SignalR server answers signalr and signalrs

## Goal

An ADR decides how `surl signalr://` and `signalrs://` answer as an ASP.NET Core SignalR hub. It covers the
negotiate request, the transports offered, the JSON hub protocol's handshake and messages, the hub's methods,
keep-alive and timeouts, authentication and limits. A server-side requirement matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specifications at dotnet/aspnetcore `main`:
  `src/SignalR/docs/specs/HubProtocol.md` (the handshake `{"protocol":"json","version":1}` with its `0x1E`
  record separator; message types 1 Invocation, 2 StreamItem, 3 Completion, 4 StreamInvocation,
  5 CancelInvocation, 6 Ping, 7 Close, and 8/9 for stateful reconnect) and `TransportProtocols.md` (negotiate
  version 1: `connectionId`, `connectionToken`, `availableTransports`; the WebSockets transport). RFC 6455 for
  WebSocket. Cite the commit of `main` read, because the documents carry no tag.
- Which requests a client sends (what to accept, not a verdict): the port's
  `Curl.Extended.Protocol.SignalR.UnitLibrary` and its matrix `SignalR.md`. Its reference hub in
  `Curl.Conformance.Extended.UnitLibrary/SignalR/` is copyable code.
- WebSocket: BL-408's library. HTTP/1.1 for negotiate: BL-407's library. JSON: the BCL's
  `Utf8JsonReader`/`Utf8JsonWriter`, or `System.Text.Json` with a source-generated context only (native AOT:
  no reflection-based serialization).

Decide and record:

1. The hub path and the hub's methods. Planning assumption: an echo method, a method that broadcasts to every
   connection on the hub, a streaming method, and a method that fails, so that every message type is reachable.
2. The transports offered in `availableTransports` (WebSockets only, or also Server-Sent Events and Long
   Polling), and the answer to a transport not offered.
3. The handshake response and its error form.
4. Keep-alive pings and the client timeout (the spec's defaults are 15 s and 30 s), on an injected `TimeProvider`.
5. Authentication: a bearer token on negotiate (header or `access_token` query), checked against the accounts
   file, with anonymous only under `--allow-anonymous`.
6. Stateful reconnect (`useStatefulReconnect`, Ack and Sequence): served or not.
7. Limits: the largest message and the connection count.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing its specification section,
      and is listed in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/SignalR.md` exists in the matrix format, each "covered by" naming
      BL-448 or BL-449, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
