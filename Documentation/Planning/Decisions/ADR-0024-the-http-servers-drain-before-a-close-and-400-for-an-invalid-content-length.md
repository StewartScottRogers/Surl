# ADR-0024 — The HTTP server's drain before a close, and 400 for an invalid Content-Length

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29
- **Supersedes:** in ADR-0019, section 6 where it says a `Content-Length` that is not one
  decimal number is served and closed unread, and section 7 ("Refused methods never read the
  body") as far as the close goes; in ADR-0008, "Refusals close" where it says a refusal is
  simply half-closed

## Context

Every response that ends a connection - a refusal (400, 405, 408, 413, 431, 501, 505), and
a response to a request that said `Connection: close`, was HTTP/1.0, or had a body the
server cannot frame - half-closes and returns, often with request bytes still unread: a
refused `POST` body, the rest of a head past the limit, a pipelined request. RFC 9112,
section 9.6, asks a server to keep reading ("lingering close") so the client's kernel does
not answer those bytes with a reset that destroys the response. ADR-0021 put a lingering
close in the TCP transport (`StreamConnection`, two seconds, no byte limit); the HTTP server
still ended its own exchange with the bytes unread, and any transport without that linger
would reset.

RFC 9112, section 6.3, item 5: a message without `Transfer-Encoding` whose `Content-Length`
is invalid has unrecoverable framing; a server answers `400 Bad Request` and closes. Until
now such a request was served and closed.

Measured on 2026-09-29 with pinned upstream curl 8.21.0 (win-x64 reference build, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`) and
`Record-CurlExchange.ps1 -Response`; the recordings are in
`Surl.Protocol.Http.UnitTests/Fixtures/`:

| Folder | Reply | curl arguments | Exit code | stderr |
| --- | --- | --- | --- | --- |
| `post-refused-405` | `405 Method Not Allowed`, `Allow: GET, HEAD`, `Connection: close` | `-sS --fail -d x` | 22 | `curl: (22) The requested URL returned error: 405` |
| `invalid-content-length-400` | `400 Bad Request`, `Connection: close` | `-sS --fail -H "Content-Length: abc"` | 22 | `curl: (22) The requested URL returned error: 400` |

curl sent the one-byte body `x` with the head of the `POST`, and sent `Content-Length: abc`
as given, with no body.

## Decision

1. **Drain after every closing response.** `HttpUnreadRequestDrainer` runs once the
   response has been written and writes completed (FIN): after a refusal that was written
   within its one-second deadline, and after a served response that closes the connection.
   It reads through the connection's `HttpConnectionReader`, so bytes already buffered go
   first, and throws them away. It does not run when a refusal missed its write deadline,
   when a transfer was aborted, or when no head arrived.
2. **Bounds: 1 second and 1 MiB.** The drain ends when the client half-closes, after
   `MaxDrainBytes` (1048576 bytes), after `MaxDrainTime` (one second, the budget ADR-0006,
   section 5 gives a refusal, timed on the exchange's `TimeProvider`), or when the
   connection fails. A bound or a failure is noted in the exchange log; the end of the
   exchange itself (its cancellation) still throws. One second is well past the 50 ms
   within which upstream curl closed after a reply in ADR-0021's runs; 1 MiB lets a refused
   upload of that size end cleanly while capping what one refused request costs the server.
   Whatever is left after either bound is the transport's to drain (ADR-0021). Both are
   fixed constants, not `ExchangeLimits` members, for the reason ADR-0021 gives.
3. **Invalid `Content-Length` is 400.** Without `Transfer-Encoding`, a `Content-Length`
   that is not exactly one field of decimal digits fitting a 64-bit length - a sign, an empty
   value, whitespace, a list such as `3, 3`, a value past `long.MaxValue`, or two fields,
   even when they agree - is `HttpRequestBodyFramingKind.InvalidContentLength`, answered
   `400 Bad Request` with `Connection: close` as a refusal, checked after the `Host` check
   and before the upload limit and method dispatch. RFC 9110, section 8.6, lets a recipient
   accept repeated identical values; rejecting them is the stricter reading, and leniency in
   framing is where two parsers disagree about where a message ends. `Transfer-Encoding`
   with `Content-Length` stays as ADR-0019, section 6 decided: served unread, then closed,
   now with the drain.

## Consequences

- A refused `POST` or `PUT` no longer risks a reset that loses its 405 or 413 on a
  transport without ADR-0021's linger; its body is read after the answer, never before, so
  `Expect: 100-continue` still gets the 413 in place of `100 Continue`.
- A client that keeps sending after a closing response holds its exchange up to one second
  longer (then up to two more in the transport).
- Fast tests: `HttpUnreadRequestDrainerTests` (the recorded refused `POST` drained before
  the close, the byte limit, the time limit on `ManualTimeProvider`, a failure and a
  cancellation while draining) and `InvalidContentLengthTests` (`abc`, `-1`, two differing
  fields, and the recorded `abc` request). `ReadCountingConnection` lets the older tests
  that proved a refusal is answered before a byte past a limit is read keep proving it.
