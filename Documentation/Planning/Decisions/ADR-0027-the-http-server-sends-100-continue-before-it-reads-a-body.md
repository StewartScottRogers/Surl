# ADR-0027 — The HTTP server sends 100 Continue before it reads a body

- **Status:** Accepted; supersedes the consequence of [ADR-0019](ADR-0019-how-the-http-server-enforces-the-hardening-limits.md) that says the server never sends `100 Continue`
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29

## Context

ADR-0019, section 5, made the HTTP server read and discard the body of a `GET` or `HEAD`
before it answers. It never sent `100 Continue`, so a client that asked for one with
`Expect: 100-continue` waited out its own timeout before it sent the body. RFC 9110,
section 10.1.1, asks a server that will read the content of such a request either to answer
at once with a final status or to send `100 Continue` first; a server that receives the
expectation in an HTTP/1.0 request must ignore it.

Measured with upstream curl 8.21.0, the win-x64 build pinned in `UpstreamCurlBuilds.json`,
through `Record-CurlExchange.ps1` (BL-083 added its `-InterimResponse` parameter), with
`-sS -X GET -H "Expect: 100-continue" --data-binary @<16-byte file>`:

| Server sends | curl |
| --- | --- |
| `HTTP/1.1 100 Continue\r\n\r\n` once the head arrived, then the 200 after the body | sent its 16-byte body at once and exited 0 after 105 ms; recorded as `Surl.Protocol.Http.UnitTests/Fixtures/expect-continue-get` |
| nothing until the body, then the 200 | sent only its 178-byte head, waited, and exited 0 after 1037 ms: its one-second `--expect100-timeout` |

## Decision

1. **When.** After method dispatch, before the server reads the body of a `GET` or `HEAD`,
   it writes exactly `HTTP/1.1 100 Continue\r\n\r\n` - no fields - when the request is
   HTTP/1.1 and one of the comma-separated values of its `Expect` fields is `100-continue`,
   compared without regard to case.
2. **Only when a body will be read.** No `100 Continue` for a request with no body
   (`Content-Length: 0` or no framing field), for a body the server does not frame (it is
   not read, ADR-0019, section 6), for an HTTP/1.0 request, or for any other expectation.
   An unknown expectation is not answered `417 Expectation Failed`: RFC 9110 lets a server
   ignore it, and no upstream curl option sends one.
3. **Refusals keep their place.** A `Content-Length` past the upload limit is still
   answered 413 before method dispatch, in place of `100 Continue` (ADR-0019, section 4),
   and a refused method is still answered before any body byte is read (ADR-0024), so
   neither sends `100 Continue`. A chunked body that goes past the limit after the
   `100 Continue` is answered 413 as before; the 413 then follows the interim response.

## Consequences

- upstream curl sends a `GET` or `HEAD` body with `Expect: 100-continue` at once instead of
  a second later.
- The interim response is written with the exchange's cancellation only, not a refusal's
  one-second deadline: it is not a refusal, and a client that stops reading is bounded by
  the engine's idle timeout and maximum exchange duration, as every other response is.
- When a request's body has already arrived with its head, the `100 Continue` is still
  sent: RFC 9110, section 15.2, obliges a client to accept an interim response it did not
  wait for.
