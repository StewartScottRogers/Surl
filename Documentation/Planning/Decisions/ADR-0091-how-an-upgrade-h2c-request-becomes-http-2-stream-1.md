# ADR-0091 — How an `Upgrade: h2c` request becomes HTTP/2 stream 1

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-369, which builds it.
- **Refines:** [ADR-0081](ADR-0081-how-surl-serves-http-2.md) decision 3's *`Upgrade: h2c`*
  bullet where it leaves a choice open, on top of
  [ADR-0090](ADR-0090-how-the-http-2-prior-knowledge-connection-reads-ends-and-answers.md).
  Every byte ADR-0081 pins is unchanged.

## Context

BL-369 answers `curl --http2 http://...`. ADR-0081 decides which requests are upgraded and what
the `101` looks like. Building it met questions that ADR leaves open: where the decision lives,
which HTTP versions may upgrade, how the bytes the HTTP/1.x reader already holds reach HTTP/2,
when stream 1 is answered, and what bytes after the `101` other than the preface mean.

Upstream curl was measured with `Record-CurlExchange.ps1` and the pinned static 8.21.0 build on
Windows. Both fixtures are in `Surl.Protocol.Http.UnitTests`:

- `Fixtures/h2c-upgrade-get-file`: `curl --http2 http://127.0.0.1:18050/file.txt` sends
  `Upgrade: h2c`, `HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA` (`MAX_CONCURRENT_STREAMS` 100,
  `INITIAL_WINDOW_SIZE` 65536, `ENABLE_PUSH` 0: 18 bytes) and
  `Connection: Upgrade, HTTP2-Settings`. After the `101` it sends the 24-byte preface, its
  `SETTINGS` (the same 18 bytes), `WINDOW_UPDATE`s and the server's `SETTINGS` ack, reads
  stream 1's answer and closes with `GOAWAY` `NO_ERROR`, last stream 0. Exit code 0.
- `Fixtures/h2c-upgrade-post-body`: `curl --http2 -d abc http://...` sends the same upgrade
  fields with `Content-Length: 3` and the body. ADR-0081 ignores the upgrade for it.

## Decision

1. **The decision is an HTTP/1.x request handler.** `Http2CleartextUpgradeHandler` wraps
   `HttpRequestAnswerer` as the cleartext connection's `IHttpRequestHandler`. A request
   `Http2CleartextUpgrade.ClientSettingsOf` accepts is answered `101 Switching Protocols`,
   `Connection: Upgrade`, `Upgrade: h2c` with an `IHttpConnectionTakeover` - the seam the
   WebSocket upgrade already uses - and every other request goes to the answerer untouched.
   `Surl.HttpServing` stays ignorant of HTTP/2 (ADR-0084).
2. **Only HTTP/1.1 upgrades.** RFC 9110 section 7.8 has a server ignore `Upgrade` on an HTTP/1.0
   request, so `HttpRequest` gains `Http1Version`, the request line's version, `null` for a
   request HTTP/2 carried. It is the one field only an upgrade decision reads.
3. **What qualifies, exactly.** `Upgrade` lists the token `h2c` (case-sensitive, as RFC 7540
   section 3.2 spells it); exactly one `HTTP2-Settings`; `Connection` lists `Upgrade` and
   `HTTP2-Settings` (case-insensitive); no `Transfer-Encoding` and no `Content-Length` other than
   `0`, so `Content-Length: 0` still upgrades. `HTTP2-Settings` must be base64url
   (RFC 7540 section 3.2.1) and decode to a payload the `SETTINGS` frame parser accepts - whole
   6-byte entries, each value in range (for example `ENABLE_PUSH` 2 is refused). Anything else
   ignores the upgrade and is answered HTTP/1.1, as ADR-0081 says. A TLS connection never gets
   the handler: `h2c` is cleartext only.
4. **The bytes after the `101`.** The HTTP/2 connection reads through
   `HttpConnectionReaderConnection`, which reads via the HTTP/1.x `HttpConnectionReader`, so the
   preface curl sent in the same packet as its request is not lost. The server's `SETTINGS` goes
   out at once after the `101`; the `HTTP2-Settings` values are applied as the client's first
   `SETTINGS` with no ack (the `101` acknowledges them, RFC 7540 section 3.2.1), so only the
   preface's own `SETTINGS` is acked. The preface is read to exactly its 24 bytes so the frames
   after it are left to the frame reader.
5. **Stream 1 waits for the preface.** The upgraded request opens stream 1, half-closed by the
   client, only once the preface is read, so its answer never precedes the client's switch to
   HTTP/2. Bytes other than the preface end the connection with `GOAWAY` `PROTOCOL_ERROR`, last
   stream 0 (stream 1 was not processed), and the note
   `after the 101 the client sent <n> bytes that are not the HTTP/2 connection preface`.
6. **Stream 1 is answered as any stream.** Its `HttpRequest` keeps the HTTP/1.1 request's method,
   target, authority and fields, with no body, and goes through the same `HttpRequestAnswerer`,
   logins included; the exchange log notes the upgrade.

## Alternatives considered

- **Upgrading a request with a body** by reading the body over HTTP/1.1 first (RFC 7540 allows
  it): rejected, ADR-0081 ignores the upgrade there, and curl completes the exchange either way.
- **Answering stream 1 straight after the `101`**, before the preface: rejected, a client that
  then sends something else would have had an answer on a connection it never switched.
- **Rejecting an HTTP/1.0 `Upgrade` with 400**: rejected, RFC 9110 section 7.8 says ignore it.

## Consequences

- `curl --http2 http://...` against `surl http://` completes on HTTP/2; every existing HTTP/1.x
  expected byte is unchanged.
- BL-370 (ALPN `h2`) reuses `Http2ConnectionServer.ServeAsync`; BL-371 measures curl's exit codes
  against a live surl, the upgrade included.
