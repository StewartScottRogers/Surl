# ADR-0186 — The HTTP server's own 4xx and 5xx refusals carry a short fixed text body

- **Status:** Accepted
- **Date:** 2026-11-20
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-20, in BL-1444.
- **Builds on:** [ADR-0008](ADR-0008-how-the-http-server-answers-get-head-and-refusals.md),
  [ADR-0019](ADR-0019-how-the-http-server-enforces-the-hardening-limits.md),
  [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 4.
- **Amends:** ADR-0008's "empty body" column and Consequences line, and ADR-0019 decision 1's "empty body".
- **Acted on by:** BL-1467 (the refusals), BL-1468 (`body=` on a fault).

## Context

Surly's bug report item 7: `curl -i .../nope.txt` shows `404` with `Content-Length: 0`, so
`--fail-with-body` keeps nothing. ADR-0008 left the question open ("a later task may add a short
fixed text body, measured against curl first"). A stand-in is used by tests that assert on what an
error looks like, and upstream curl's own manual says a failing server "returns an HTML document".

## Measurement

Pinned upstream curl 8.21.0 (win-x64), `Record-CurlExchange.ps1`, loopback, 2026-11-20:

| Canned answer | Arguments | Exit | stdout |
|---|---|---|---|
| `404`, `Content-Type: text/plain; charset=utf-8`, `Content-Length: 14`, body `404 Not Found\n` | `-sS --fail-with-body` | 22 | `404 Not Found\n`; stderr `curl: (22) The requested URL returned error: 404` |
| the same | `-sS -i` | 0 | the head, a blank line, then `404 Not Found\n` |
| `404`, `Content-Length: 0` | `-sS --fail-with-body` | 22 | empty; the same stderr |
| `404`, `Content-Length: 0` | `-sS -i` | 0 | the head and the blank line only |

A body changes only what curl keeps; the exit code and stderr are the same. An empty body is
therefore a valid answer, and a body is a strict gain for a test that wants to read it.

## Decision

1. **Which responses.** Every 4xx and 5xx the HTTP server (`Surl.Protocol.Http`, over
   `Surl.HttpServing`) originates itself carries a body: the 404, 405, 501, 400, 401, 403,
   408, 412, 413, 416, 431, 505 and 503 rows of ADR-0008, ADR-0019 and later ADRs, and a
   `--fault error` answer.
2. **Exact bytes.** The status code, a space, the reason phrase from the server's own status
   table, then one `\n` (`404 Not Found\n`, 14 bytes). `Content-Type: text/plain; charset=utf-8`
   (ADR-0142's `charset` rule is for stored files and does not apply to a generated text) and
   `Content-Length` equal to the body. No HTML: nothing in it can be mistaken for markup or echo
   the request, so it cannot become a reflected-content hole.
3. **Never a body.** `HEAD` (the head is the same as GET's, `Content-Length` included, and no
   body byte follows, RFC 9110 section 9.3.2), every status below 400, `304` and `204`.
4. **Left empty.** The extended servers (S3, Azure, Cloud Storage and the like, which already
   write their own protocol error documents or none), the proxy, RTSP and WebSocket refusals:
   none is a path `curl --fail-with-body` is used against in an HTTP file site. They may adopt
   the same body in their own tasks.
5. **A fault chooses it.** `--fault error;scheme=http;reply=<status>` answers with decision 2's
   body; an optional `body=<text>` replaces the text (same `Content-Type`, none for HEAD).
6. **Where the refusal closes the connection** (`Connection: close`, half-closed) the body is
   written before the close, within the one-second refusal deadline of ADR-0019.

## What a body changes, and why each stays right

- Fixtures under `Protocols/Http/Surl.Protocol.Http.UnitTests/Fixtures` that pin an error:
  `not-found-fail`, `anonymous-401-fail`, `basic-401`, `bearer-401`, `basic-plaintext-403`,
  `post-refused-405`, `etag-compare-412` and `-412-fail`, `time-cond-until-412-fail`,
  `expect-continue-413`, `head-too-large-431`, `head-timeout-408`, `invalid-content-length-400`,
  `fault-503-fail`, `fault-503-then-get`, `refusal-503`. Each pins what curl sees of the answer:
  a `--fail` run still exits 22 with the same stderr (measured above), and a `-i` run gains the
  body. They are re-recorded from pinned upstream curl in BL-1467; the exit codes do not change.
- `HEAD` fixtures (`head`, `head-file`) and `304` fixtures (`etag-compare-304`) stay: decision 3.
- Unit tests that assert `Content-Length: 0` on a refusal (`Http1AnswerWriterTests`,
  `ConnectionRefusalTests`, `AuthenticationTests`, `ExpectContinueTests`) change with the
  refusal; those on a non-error answer stay.
- Upstream test cases under `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases` that
  target Surl as the server run curl against it and check curl's exit code and output, which a
  body does not alter for a non-`--fail-with-body` run; any that pin a refusal's output are
  updated in BL-1467 from the same upstream expectation.

## Consequences

- ADR-0008's Consequences line is closed by this ADR; its table's "empty body" is superseded
  for the servers decision 1 names.
- Two tasks carry it out: BL-1467 and BL-1468.
