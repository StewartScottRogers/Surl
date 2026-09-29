# ADR-0019 — How the HTTP server enforces the hardening limits

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29
- **Supersedes:** in ADR-0008, the `431` row, the persistence rule for a GET or HEAD that
  announces a body, and "Refusals close" where it says every refusal is simply half-closed

## Context

ADR-0006 fixed the limits every server enforces and what each hit is answered with
(sections 1, 3 and 5); BL-046 put them on `ExchangeContext.Limits` and added
`IConnectionRefusalWriter`. BL-050 builds them into `Surl.Protocol.Http`. ADR-0006 leaves
open where in the HTTP server each check sits, how a request body is read so a chunked
one can be counted, and what the server does with a body it does not keep. Until BL-050
the server read no request body at all and closed the connection after any request that
announced one (ADR-0008).

Every new reply below was fed to pinned upstream curl 8.21.0 (win-x64 reference build,
SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`) with
`Record-CurlExchange.ps1 -Response` on 2026-09-29 before a test pinned it; the recordings
are in `Surl.Protocol.Http.UnitTests/Fixtures/` and listed in its `README.md`:

| Reply | curl arguments | Exit code | stderr |
| --- | --- | --- | --- |
| `200 OK` with `Server: surl` | (none), and `-I` | 0 | progress meter only |
| `404 Not Found` with `Server: surl` | `--fail` | 22 | `curl: (22) The requested URL returned error: 404` |
| `408 Request Timeout`, `Connection: close` | `-sS --fail` | 22 | `curl: (22) The requested URL returned error: 408` |
| `431 Request Header Fields Too Large`, `Connection: close` | `-sS --fail` | 22 | `curl: (22) The requested URL returned error: 431` |
| `413 Content Too Large`, `Connection: close` | `-sS --fail --data-binary @<2048-byte file>` | 22 | `curl: (22) The requested URL returned error: 413` |
| the same 413, sent once the head arrived (`-RespondAfterBodyBytes 0`) | `-sS --fail -H "Expect: 100-continue" --data-binary @<2048-byte file>` | 22 | the same; curl sent the 181-byte head and no body byte |
| `503 Service Unavailable`, `Connection: close`, no `Date` | `-sS --fail` | 22 | `curl: (22) The requested URL returned error: 503` |

## Decision

1. **`Server: surl` on every response**, right after `Date` (or first, where there is no
   `Date`), with no version (ADR-0006, section 3). Every error response has an empty body
   and its reason phrase from the server's own status table.
2. **Head timeout.** The first head is timed from the start of `ServeAsync`; a later head
   on a kept-alive connection from its first byte, found by waiting for one byte with only
   the exchange's cancellation (the engine's idle timeout). Running out part way through a
   head is answered `408 Request Timeout` as a refusal; running out before any byte of the
   head closes with no bytes. The timer is a `CancellationTokenSource` on the exchange's
   `TimeProvider`; `Timeout.InfiniteTimeSpan` never fires, and neither does a timeout longer than a timer can wait (`uint.MaxValue - 1` milliseconds, about 49.7 days), which is treated as none rather than let the timer throw.
3. **Head size.** `MaxRequestHeadBytes` replaces the reader's fixed 300 KiB. The reader
   never takes more than the limit of a head from the connection. 0 means no limit, which
   in practice is `Array.MaxLength`, the most one buffer holds.
4. **Where the upload check sits.** After the `Host` check and **before method dispatch**:
   any method whose single valid `Content-Length` is past `MaxUploadBytes` is answered
   `413 Content Too Large` before a body byte is read, so `Expect: 100-continue` gets the
   413 in place of `100 Continue`.
5. **Bodies the server reads.** After dispatch, the body of a `GET` or `HEAD` is read and
   discarded before the answer, and the connection then stays open as ADR-0008's
   persistence rule decides. A chunked body counts its chunk data and its trailer field
   lines against `MaxUploadBytes` and is answered 413 as soon as a chunk-size line or a
   trailer line takes the count past it, before that chunk's data is read. A chunk-size
   line or trailer line may take 8192 bytes (ADR-0006's line limit), its line ending
   included. Chunk lines are parsed strictly, because leniency is where two parsers disagree
   about where a message ends: every chunk-size line, the line after chunk data and every
   trailer line must end with CRLF (a bare LF is malformed), and whitespace is allowed only
   between the size and a `;` (RFC 9112, section 7.1.1). A malformed chunked body (a size
   that is not hex, whitespace before or after a size with no `;`, one of 16 digits with the
   top bit set, chunk data not followed by CRLF, a bare LF, a line past 8192 bytes) and a body the client
   stops sending before its end are answered `400 Bad Request` as a refusal.
6. **Bodies the server does not frame.** `Transfer-Encoding` on an HTTP/1.0 request (RFC 9112,
   section 6.1, makes it badly framed, and reading it would let a keep-alive HTTP/1.0 client
   smuggle a second request), another transfer coding, a coding list, two
   `Transfer-Encoding` fields, `Transfer-Encoding` with `Content-Length`, or a
   `Content-Length` that is not one decimal number: the body is not read, the request is
   answered, and the connection closes, as before. BL-061 decides the 400 for an invalid
   `Content-Length` and the drain before a close.
7. **Refused methods never read the body**, as before (BL-061 drains it).
8. **Refusals get one second.** Every refusal (400, 405, 408, 413, 431, 501, 505) is
   written and half-closed within a one-second deadline on the exchange's clock; past it
   the server notes it, stops, and leaves the close to the engine's dispose. It never
   aborts for a limit.
9. **Connection refusal.** `HttpProtocolServer` implements `IConnectionRefusalWriter`:
   both `ConnectionRefusal` values write `503 Service Unavailable`, `Server: surl`,
   `Content-Length: 0`, `Connection: close`, then half-close. The 503 carries no `Date`:
   the interface hands the server no clock, and RFC 9110, section 6.6.1, makes `Date`
   optional on a 5xx. The engine bounds the write with its own one-second deadline.

## Consequences

- A `GET` or `HEAD` with a body no longer costs the client its connection.
- The head timeout does not bound a body (ADR-0006, section 1), so a client trickling a body
  it is allowed to send is bounded only by the engine's idle timeout and maximum exchange
  duration, as any slow upload is; with `--max-filesize 0` it may send forever within them.
- The server still never sends `100 Continue`; curl sends the body after its one-second
  `--expect100-timeout`, which the discard then reads. Sending `100 Continue` before
  reading a GET's or HEAD's body is left to a follow-up task.
- A file-system failure while answering still escapes `ServeAsync` with no byte written;
  BL-060 decides its answer, which must also be fixed text.
