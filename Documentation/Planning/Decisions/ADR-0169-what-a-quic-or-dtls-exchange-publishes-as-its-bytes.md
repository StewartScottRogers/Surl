# ADR-0169 — What a QUIC or DTLS exchange publishes as its bytes

- **Status:** Accepted
- **Date:** 2026-11-16
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-16, in BL-1243
  (the second stand-in audit).
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) (decision 1: every exchange's bytes are
  published and read by recording, `Surl.Mcp` and the control endpoint), ADR-0004 section 5 (every byte reaches
  `IExchangeLog`), ADR-0033 (`--trace`, `--trace-ascii`), ADR-0003 (observation only: nothing here is a byte
  upstream curl sends or expects).
- **Amends:** ADR-0124 decision 1 (`ExchangeBytesEvent` gains a layer and a stream identifier; a QUIC or DTLS
  exchange now publishes what the client sent, not only protected packets).
- **Amended by:** [ADR-0182](ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md)
  amendment 22 (decision 1's header text writes a CR or LF inside a name or value as `\r` or `\n`).
- **Acted on by:** the follow-up tasks in decision 6.

## Context

Over TCP, TLS sits inside the `IConnection` the engine wraps in `RecordingConnection`, so the published bytes
are plaintext. Over UDP the engine wraps the flow in `RecordingDatagramFlow`, but QUIC (`Surl.Quic`) and DTLS
(`Surl.Dtls`) run inside the server, beneath that recording. An exchange on `https` with `--http3`,
`webtransport`, `dns` over QUIC and `coaps` therefore publishes protected packets only: a test cannot read a
request path, a DNS question or a CoAP request back. Nothing outside `Surl.Core` calls
`IExchangeLog.BytesReceived` or `BytesSent`.

## Decision

### 1. What is published

Protected packets stay published exactly as today, as the wire layer. In addition, each exchange publishes
what the protocol layers below the application recovered, in two further layers:

- **Decrypted**: the plaintext the client sent and the server sent, never reordered or reframed by Surl:
  - QUIC: each STREAM frame's payload, in stream order, and each DATAGRAM frame's payload, with the QUIC stream
    identifier (a datagram has none). Frame headers, ACKs, CRYPTO data and other QUIC control frames are not
    published. So a DNS-over-QUIC question (RFC 9250: two-byte length, then the message) and a WebTransport
    stream's bytes appear exactly as the client wrote them.
  - DTLS: each application-data record's plaintext (for `coaps`, the CoAP message). Handshake and alert records
    are not published.
- **Decoded**: for HTTP/3 (including the WebTransport CONNECT request) the decoded request or response header
  list, one `name: value` line each ending CRLF, pseudo-headers first in the order received, then a blank line;
  each DATA frame's payload is then published as Decrypted bytes on the same stream. QPACK makes the
  stream bytes unreadable on their own, which is why this layer exists; this is what a test reads `:path` from.
  It is rendered text, not wire bytes, and says so by its layer.

A choice is not offered: all layers are published while `IsPublishingBytes` is true, and a reader filters by
layer.

### 2. The contract (changes only add)

- `ExchangeBytesLayer` enum in `Surl.Protocol.Abstractions`: `Wire` (0), `Decrypted`, `Decoded`.
- `ExchangeBytesEvent` gains the init properties `Layer` (default `Wire`) and `StreamId` (`long?`, default
  `null`). Its positional constructor does not change, so every existing publisher and reader is unaffected.
- `ExchangeEndedEvent`'s received and sent counts stay wire bytes only, so no count changes.
- `Surl.Api`'s `SurlExchangeRecord`, `get_exchanges` and `GET /v1/exchanges` gain a per-chunk `layer` and
  `streamId` field next to the existing direction and bytes; a reader that ignores them sees the wire chunks as
  before. Each stream's chunks keep arrival order within their layer.

### 3. The seam

`Surl.Quic` and `Surl.Dtls` reference `Surl.Protocol.Abstractions` already and never `Surl.Core`. Abstractions
gains `IDecryptedBytesSink` (`Received(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes)` and
`Sent(...)`, with a discarding default `DiscardingDecryptedBytesSink.Instance`) and `ExchangeContext` gains the
init property `DecryptedBytes`, default discarding. The engine sets it in `OpenExchange` to a sink that publishes
an `ExchangeBytesEvent` with the layer and stream, and writes it to the exchange log for `--trace`. The server
hands the sink to the QUIC or DTLS code it constructs, as it already hands them its clock; they call it and
nothing else changes. No static state; many instances run side by side.

### 4. `--trace` and `--trace-ascii`

The trace shows the same bytes: the wire chunks as today, and the Decrypted and Decoded chunks as further
records, each titled with its layer and, for QUIC, `stream <id>`, in the trace's existing hex or ASCII dump
form. The trace's wire records are unchanged.

### 5. Secrets

Never published, in any layer: TLS 1.3 and DTLS handshake messages and secrets, traffic keys, IVs, header
protection keys, QUIC connection IDs' retry and stateless-reset tokens, session tickets and NEW_TOKEN values,
PSKs, and any private key. Decrypted and Decoded carry only application data a client or the server sent. The
bytes a test sent in its own request (including an `authorization` header it set) are published as they are
today over TLS on TCP: that is the exchange, not a secret of the server.

### 6. No byte a client receives changes

This is observation only. Every packet, record and frame Surl sends is the same bytes as before; a publishing
call is a copy made after the protocol layer has decided what to send, and costs nothing while
`IsPublishingBytes` is false (the default sink and a non-publishing engine skip the copy).

### 7. Follow-up tasks

One feature task per library, in order: the contract in `Surl.Protocol.Abstractions` first; then the engine
(`Surl.Core`); then `Surl.Quic`, `Surl.Dtls` and `Surl.Http3` (each calls the sink); then the four servers that
hand the sink in (`Surl.Protocol.Http` for `--http3`, `Surl.Extended.Protocol.WebTransport`,
`Surl.Extended.Protocol.DnsQuery`, `Surl.Extended.Protocol.Coap`); then the host proof in
`Surl.Composition.UnitTests`; then the hosts' `layer` and `streamId` fields and their `--aihelp` text.

## Alternatives considered

- **Move QUIC and DTLS into the transport so the engine records plaintext.** Rejected: the server owns the
  handshake and fault points (ADR-0124 decision 4), and it would move hand-built crypto into `Surl.Networking`.
- **Publish only HTTP/3 reassembled requests.** Rejected: DNS-over-QUIC, WebTransport and coaps have no
  HTTP/3 request, and the decrypted stream is the common denominator.
- **Publish the protected packets only, as now.** Rejected: unreadable, which defeats decision 1.
- **A new event type.** Rejected: readers already filter `ExchangeBytesEvent`; adding properties is smaller.

## Consequences

- A test reads back an HTTP/3 request's `:path`, a DNS-over-QUIC question or a CoAP request through the same
  exchange record as for TLS over TCP.
- The wire layer stays as it was, so existing recordings and counts do not change.
