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
completed: 2026-11-05
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

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing its specification section,
      and is listed in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/SignalR.md` exists in the matrix format, each "covered by" naming
      BL-448 or BL-449, or "n/a: <reason>".

## Notes

- ADR-0119 records decisions 0 to 7. Specifications read at dotnet/aspnetcore `main` commit
  `6aacb16a27a677d16278a9814ed04f871020854b` (2026-11-05); `src/SignalR/docs/specs` last changed in
  `fa8126f62f64eaf37292ff1e334ace99bc757bcf` (2026-07-02).
- Hub methods: `Echo`, `Broadcast`, `Count` (paced stream, 100 ms), `Collect` (upload streaming, so HUB "Upload
  streaming" is reachable too) and `Fail`.
- The default rule ("nothing left out") meant Server-Sent Events, Long Polling, stateful reconnect and MessagePack
  are all served, not refused. To keep BL-448 and BL-449 sized for one run each, they were filed as BL-754
  (SSE and Long Polling with HTTP Post), BL-755 (stateful reconnect) and BL-756 (MessagePack), each depending on
  BL-449; `availableTransports` lists only what is served at the time.
- The keep-alive 15 s and client timeout 30 s are not numbers in the specifications; they are ASP.NET Core's
  documented defaults, as the task's planning assumption said.
- The port's client sends no credentials on its first negotiate, so BL-451 must run it against
  `surl --allow-anonymous`; recorded in ADR-0119's Context.
- The matrix's rows for the later tasks are "n/a: BL-75x adds ..." so every "Covered by" names BL-448, BL-449 or
  n/a, as the acceptance criterion asks; the TLS row is n/a because the listener seam wraps TLS (BL-450 proves it).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0119 decides how the SignalR server answers signalr and signalrs; matrix SignalR.md traces it to BL-448/BL-449; BL-754 to BL-756 filed
