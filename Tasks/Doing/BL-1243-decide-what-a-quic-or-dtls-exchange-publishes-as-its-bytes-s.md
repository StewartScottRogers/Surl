---
id: BL-1243
title: Decide what a QUIC or DTLS exchange publishes as its bytes, so a test can read what an HTTP/3, WebTransport, DNS-over-QUIC or coaps client sent
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-060
created: 2026-11-16
completed:
---
# BL-1243 — Decide what a QUIC or DTLS exchange publishes as its bytes, so a test can read what an HTTP/3, WebTransport, DNS-over-QUIC or coaps client sent

## Goal

An ADR, decided by Claude under Stewart's delegation, says what `ExchangeBytesEvent`s an exchange carried over QUIC or DTLS publishes, so `Surl.Api`'s `Exchanges`, `Surl.Mcp`'s `get_exchanges` and the control endpoint's `GET /v1/exchanges` show a test the decrypted bytes the client sent, as they already do for TLS over TCP. The follow-up tasks are filed.

## Context

- Found by the second stand-in audit (2026-11-16). ADR-0124 decision 1 says each instance publishes every exchange's bytes; recording, `Surl.Mcp` and the control endpoint read them.
- Over TCP, TLS is inside the `IConnection` the engine wraps in `Foundation/Surl.Core.UnitLibrary/RecordingConnection.cs` (`ServingEngine.cs` line 734), so the published bytes are the plaintext the server reads and writes.
- Over UDP, `ServingEngine.Datagrams.cs` line 85 wraps the flow in `RecordingDatagramFlow`, and the QUIC handshake and packet protection (`Protocols/Http/Surl.Quic.UnitLibrary`) and DTLS (`Cryptography/Surl.Dtls.UnitLibrary`, used by `Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary`'s `SecureCoapProtocolServer`) run inside the server, beneath the recording. So an exchange on `https` with `--http3`, `webtransport`, `dns` with `--dns-transport quic`, and `coaps` publishes only protected packets: a test cannot read a request header, a WebTransport payload, a DNS question or a CoAP request back. No production code outside `Surl.Core` calls `IExchangeLog.BytesReceived`/`BytesSent`.
- Questions the ADR answers:
  1. What is published for such an exchange: the decrypted stream and datagram payloads (per QUIC stream? HTTP/3 frames or the HTTP/3 request reassembled?), the DTLS record plaintext, the protected packets as well, or a choice?
  2. Whether `ExchangeBytesEvent` (`Foundation/Surl.Protocol.Abstractions.UnitLibrary`) needs a stream identifier or a layer marker; any contract change is its own task first. A change only adds (ADR-0124: the configuration model and contracts only grow).
  3. Where the seam lives so `Surl.Quic` and `Surl.Dtls` reach the exchange's publishing log without referencing `Surl.Core`, and how `--trace` and `--trace-ascii` (ADR-0033) show the same bytes.
  4. Secrets: TLS 1.3 secrets, keys and tokens are never published (ADR-0124 decision 1); say what that excludes.
- No byte a client sees changes; this is observation only. State that in the ADR.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/`, with the next free number, marked "Decided by Claude under Stewart's delegation", answers questions 1 to 4 and states that no byte a client receives changes.
- [ ] ADR-0124 carries an "Amended by" line naming the new ADR, if decision 1 changes.
- [ ] One feature task per library the ADR changes is filed with `task-board.ps1 new`, depending on this task, with exact `touches`; a contract task in `Surl.Protocol.Abstractions.UnitLibrary` comes before the `Surl.Quic`, `Surl.Http3`, `Surl.Dtls` or server tasks that use it. Each is listed in Notes.
- [ ] One follow-up proves it from a host: a `Surl.Api.UnitTests` or `Surl.Composition.UnitTests` test reading an HTTP/3 request's `:path` back from `SurlServer.Exchanges` (or the composition's event stream), named in that task's criteria.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
