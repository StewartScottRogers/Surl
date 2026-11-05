---
id: BL-401
title: Decide the libraries the extended servers share for HTTP/1.1, WebSocket and DTLS
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-543]
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-401 — Decide the libraries the extended servers share for HTTP/1.1, WebSocket and DTLS

## Goal

An ADR amends ADR-0002 decision 3's table of horizontal libraries. It decides how the extended servers share
the HTTP/1.1 request handling, the WebSocket machinery and a hand-built DTLS 1.2 without referencing an original
protocol server, and where each shared library goes.

## Context

- Isolation (BL-543's ADR, and ADR-0002 decision 3): an extended server may reference only
  `Surl.Protocol.Abstractions` and the horizontal libraries in ADR-0002's table, as amended. ADR-0070 is the
  model: it moved the HTTP/1.x message machinery out of `Surl.Protocol.Http` into `Surl.HttpMessage`, so that
  the WebSocket and RTSP servers could share it.
- **HTTP/1.1.** S3, AzureStorage, GoogleCloudStorage, OciRegistry and McpServer (Streamable HTTP) all ride on
  HTTP/1.1. So do the SignalR negotiate request, DNS over HTTPS, and the WebSocket upgrade under SignalR,
  SocketIo and Mqtt5's `mqtt5+ws`.
  - `Surl.HttpMessage` already holds the bounded head reader, request-line and field parsing, `HttpResponseHead`,
    `HttpStatus` and `HttpRequestBodyFraming`.
  - Still inside `Protocols/Http/Surl.Protocol.Http.UnitLibrary`: request bodies (`Content-Length` and chunked,
    `100 Continue`), response writing (`ConnectionWriteStream`), keep-alive (`HttpConnectionPersistence`),
    draining (`HttpRequestBodyDiscarder`, `HttpUnreadRequestDrainer`) and the lingering close.
  - BL-366 splits the HTTP server's answering from HTTP/1.x framing. Decide what moves after it, to which
    library (into `Surl.HttpMessage`, or a new `Surl.HttpServing`), and the one request-handler shape an
    extended HTTP server implements. BL-407 builds it.
- **WebSocket.** `Protocols/Http/Surl.Protocol.Ws.UnitLibrary` holds `WebSocketAcceptKey`, `WebSocketUpgradeChecks`,
  `WebSocketUpgradeResponder`, `WebSocketFrameReader`, `WebSocketFrameEncoder`, `WebSocketMessageReassembler`,
  `WebSocketCloseCodes`, `WebSocketLingeringClose` and their outcome types (ADR-0071). Decide which move to a
  new horizontal library (planning name `Surl.WebSocket`, in `Protocols/Http/`) and how a server names its
  subprotocol (`mqtt` for MQTT 5 chapter 6). The WebSocket server's bytes must not change. BL-408 builds it.
- **DTLS 1.2.** `coaps` needs DTLS 1.2 (RFC 6347). The BCL has no DTLS. Decide its library (planning name
  `Surl.Dtls`; in `Cryptography/` as a hand-built primitive, or `Foundation/` as a service; ADR-0079
  decision 3), its references (`Surl.Protocol.Abstractions` for `IDatagramFlow`), and its row. BL-403
  decides the handshake itself.
- **SCRAM.** XMPP needs SCRAM-SHA-1 and SCRAM-SHA-256 SASL (RFC 5802, RFC 7677), which `Surl.Authentication`
  lacks today; BL-416 adds them there. Kafka, AMQP and STOMP logins use the existing
  `ISaslAuthenticationPolicy` and `IAuthenticationPolicy` in `Surl.Protocol.Abstractions`. Confirm that an
  extended server reaches every login through those contracts, as the original servers do.
- **Object stores.** S3, AzureStorage, GoogleCloudStorage and OciRegistry all store blobs. Decide whether each
  maps buckets, containers and repositories onto `Surl.Content`'s content store (already a horizontal library),
  or whether a shared object-store library is warranted.
- HTTP/2 and HTTP/3 sharing (gRPC, WebTransport, DNS over QUIC) is decided separately by BL-402, once
  BL-364 and BL-372 have decided how they are built.
- `Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs` holds ADR-0002's table as code.
  Each new row joins it in the task that creates the library.

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". For each shared library (HTTP/1.1 serving, WebSocket, DTLS) it names
      the library, its folder, what moves into it type by type, its row in ADR-0002's table (what it may
      reference), and the task that builds it. It states the object-store and SCRAM decisions, and is listed in
      the folder's `README.md`.
- [x] ADR-0002 carries an "Amended by" line pointing to the new ADR.
- [x] `Documentation/Product/Product-Overview.md`, "Architecture" (Rule 1 and the "Layers" table), names the new
      libraries.

## Notes

- 2026-11-04: Written in the session rather than delegated to `align-and-document`: the work was
  one ADR plus three one-line edits, and the decisions needed the facts already read.
- Decided in ADR-0084: `Surl.HttpServing` (new, `Protocols/Http/`; Abstractions, `Surl.HttpMessage`,
  `Surl.Content`) rather than growing `Surl.HttpMessage`, because RTSP shares `Surl.HttpMessage`
  and must not carry HTTP/1.1 connection semantics; it references `Surl.Content` so ADR-0081
  decision 8's `HttpAnswerBody` (content-store file kind) moves unchanged. Handler shape
  `IHttpRequestHandler.AnswerAsync(HttpRequest, CancellationToken)` returning `HttpAnswer`, run by
  `Http1ConnectionServer`; upgrades hand over through an `IHttpConnectionTakeover`.
- `Surl.WebSocket` (`Protocols/Http/`; Abstractions, `Surl.HttpMessage`): 17 types move,
  responder/exchange/served entry/server stay; `WebSocketUpgradeAcceptance` names the first of the
  server's subprotocols the client offers, never refuses itself.
- `Surl.Dtls` in `Cryptography/` (Abstractions only), beside `Surl.Tls13` (ADR-0082 precedent).
- Logins through existing contracts; SCRAM in `Surl.Authentication` (BL-416). Object stores on
  `Surl.Content`, no shared library.
- Docs only: no `.cs` or project file touched, so `verify` was not needed; build and fast tests
  run anyway as the shift requires.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. ADR-0084 decides Surl.HttpServing, Surl.WebSocket and Surl.Dtls, their rows, contents and building tasks
