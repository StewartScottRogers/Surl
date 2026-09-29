---
id: BL-050
title: Enforce the head timeout, head size and upload limit and answer refusals in Surl.Protocol.Http
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-018, BL-046]
touches: [Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-050 — Enforce the head timeout, head size and upload limit and answer refusals in Surl.Protocol.Http

## Goal

The HTTP server (BL-018) enforces `ExchangeContext.Limits` - the head timeout (408 or a
bare close), the 100 KiB request head (431) and the upload limit (413, including in
place of `100 Continue`) - implements `IConnectionRefusalWriter` with a 503, sends only
fixed error text, and names itself `Server: surl` with no version; every new reply is one
pinned upstream curl 8.21.0 was fed and recorded.

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0006-hardening-for-internet-facing-use.md`,
  sections 1 (head timeout, "Maximum request head", "Maximum upload", and "Sizes"),
  3 (fixed error text, no version in a banner) and 5 (the HTTP column, and "Every hit is
  a graceful close"). The contract types come from BL-046 (`ExchangeLimits`,
  `ExchangeContext.Limits`, `IConnectionRefusalWriter`, `ConnectionRefusal`).
- Head timeout: `HeadTimeout`, timed on `ExchangeContext.TimeProvider` (for example a
  `CancellationTokenSource` built with it). For the first head the clock starts when
  `ServeAsync` starts (the TLS handshake before it is bounded by `Surl.Networking`,
  BL-048); on a kept-alive connection it starts at the first byte of the next head, and
  until then only `Surl.Core`'s idle timeout applies. When it fires: if any byte of the
  head arrived, `408 Request Timeout` with `Connection: close`, then close; otherwise close
  with no bytes.
- Head size: counted from the first byte through the terminating `CRLF CRLF` inclusive.
  Past `MaxRequestHeadBytes`: `431 Request Header Fields Too Large`, `Connection: close`,
  then close. Never read more than the limit to be polite.
- Upload: a request body the server reads (to answer it, or to discard it on a kept-alive
  connection) that declares a `Content-Length` over `MaxUploadBytes`, or whose chunked
  length passes it, gets `413 Content Too Large`, `Connection: close`, then close. A
  request with `Expect: 100-continue` and a declared length over the limit gets the 413
  instead of `100 Continue`, before any body byte is read. State in the server's XML doc
  where the check sits relative to method dispatch. Uploads written to the store
  (`PUT`/`POST`, 405 with `Allow` when off) are a later HTTP task, not this one.
- Refusal: implement `IConnectionRefusalWriter` on the HTTP server; both
  `ConnectionRefusal` values write `503 Service Unavailable` with `Content-Length: 0` and
  `Connection: close`. ADR-0006 already recorded that the pinned build exits 22 on this
  with `-sS -f`, stderr `curl: (22) The requested URL returned error: 503`.
- Every refusal is written with a one-second write deadline, then writes are completed
  and the connection disposed; never `Abort` for a limit.
- Error text: every error response's reason phrase and body come from the server's own
  table. None contains a local path, the served root, an exception message or type, an
  OS error text or number, or a host user name. Every response carries `Server: surl`
  exactly - no surl, .NET or OS version.
- Measurement (ADR-0003): feed each new response - 408, 431, 413, 413 in place of
  `100 Continue` (curl sent `-H "Expect: 100-continue" --data-binary @<file>`), 503, and a
  200 carrying `Server: surl` - to the pinned build with `Record-CurlExchange.ps1
  -Response` (and `-RespondAfterBodyBytes 0` for the early 413), and commit each recording
  under `Surl.Protocol.Http.UnitTests/Fixtures/<case>/` as `EmbeddedResource` with the
  command line and build SHA-256 in its `README.md`. The expected bytes in the tests are
  the recorded ones curl was fed; exit codes and stderr are whatever the build reported.
- Tests use a hand-written `TimeProvider` in `Surl.Protocol.Http.UnitTests` (no package),
  and BL-005's `InMemoryConnection` with `peerHalfClosesWhenExhausted: false` for a peer
  that stalls.

## Acceptance criteria

- [x] `HeadTimeoutTests.PartialHead_AfterHeadTimeout_Answers408AndCloses` and
      `HeadTimeoutTests.NoBytes_AfterHeadTimeout_ClosesWithNoBytes` pass, and a test proves
      a kept-alive connection's idle wait before the next head is not cut off by
      `HeadTimeout`.
- [x] `RequestHeadLimitTests.HeadOfExactlyTheLimit_IsAnswered` and
      `RequestHeadLimitTests.HeadOneByteOverTheLimit_Answers431AndCloses` pass, and a
      test proves `MaxRequestHeadBytes = 0` accepts a 200 KiB head.
- [x] `UploadLimitTests.ContentLengthOverTheLimit_Answers413AndCloses`,
      `UploadLimitTests.ChunkedBodyOverTheLimit_Answers413AndCloses` and
      `UploadLimitTests.ExpectContinueOverTheLimit_Answers413InsteadOf100Continue` pass,
      the last proving no body byte was read.
- [x] `ConnectionRefusalTests` prove both `ConnectionRefusal` values write the exact 503
      bytes and complete writes without `Abort`.
- [x] A test with a content-store fake that throws an `IOException` whose message holds a
      path proves the response contains neither the path nor the message, and a test
      proves every response carries `Server: surl` exactly.
- [x] Recordings for each case above are committed; each test's expected bytes equal
      the bytes that recording fed to pinned upstream curl 8.21.0.
- [x] `dotnet build Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, the fast tests
      are green with no `Integration` test in `Surl.Protocol.Http.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Http.UnitLibrary`.

## Notes

Plan and decisions (2026-09-29), recorded in ADR-0019 ("Decided by Claude under Stewart's
delegation"), which supersedes ADR-0008's 431 row and body-persistence rule:

- `touches` gained `Documentation/Planning/Decisions` for ADR-0019 (and the one-line
  supersession note on ADR-0008 and the index); no task in `Doing` names it.
- `Server: surl` goes right after `Date` in every response. That changed the three BL-018
  response recordings, so `get-file`, `head-file` and `not-found-fail` were re-recorded
  with it (port 18050) and five new ones added: `head-timeout-408`, `head-too-large-431`,
  `upload-too-large-413`, `expect-continue-413` (`-RespondAfterBodyBytes 0`; curl sent the
  181-byte head and no body), `refusal-503`. Every one exited as ADR-0019's table says.
- Head timeout: a `CancellationTokenSource` on the exchange `TimeProvider`, started at
  `ServeAsync` for the first head and at the first byte (`HttpConnectionReader.WaitForBytesAsync`)
  for later ones. `HttpRequestHeadReadOutcome` gained `HeadTimedOut` (408) and
  `HeadTimedOutBeforeAnyByte` (bare close).
- Head size: `HttpConnectionReader` takes `MaxRequestHeadBytes` instead of its 300 KiB
  constant and caps every read at what is left of the limit, so it never takes more of a
  head than the limit. 0 is `Array.MaxLength` in practice.
- Upload limit: a declared `Content-Length` is checked after the `Host` check and before
  method dispatch (so `Expect: 100-continue` gets the 413 instead of `100 Continue`, and a
  `POST` over the limit gets 413, not 405). To count a chunked body the server now reads
  and discards `GET`/`HEAD` bodies (`HttpRequestBodyDiscarder`) and keeps the connection
  after them; a chunked body counts chunk data plus trailer lines, chunk lines are capped at
  8192 bytes, and a malformed or truncated body is 400. Framings it cannot read stay as
  before (served, then closed) and are BL-061's.
- Every refusal (400/405/408/413/431/501/505) gets a one-second write deadline on the
  exchange clock; a missed deadline is noted and the close left to the engine's dispose,
  never `Abort`.
- The 503 carries no `Date`: `IConnectionRefusalWriter` hands the server no clock and
  RFC 9110 makes `Date` optional on a 5xx. The engine already bounds the refusal write.
- A content-store `IOException` still escapes `ServeAsync` with no byte written; the test
  proves nothing of it reaches the client either way. Its answer is BL-060's.
- Code review (2026-09-29) fixes: HTTP/1.0 with `Transfer-Encoding` is unreadable framing (RFC 9112 section 6.1, a smuggling route); a head timeout longer than a timer can wait (~49.7 days) is treated as none instead of throwing; chunk lines must end in CRLF with no whitespace before the size or after it without `;`. The body discard is bounded only by the engine's idle timeout and maximum duration, as ADR-0006 intends; ADR-0019 records it.
- Verified: `Measure-CodeQuality.ps1` reports 100% line and branch coverage and 0 failing members in `Surl.Protocol.Http.UnitLibrary`; the library builds clean with `-warnaserror`; all 14 fast test assemblies pass (286 tests in `Surl.Protocol.Http.UnitTests`, none `Integration`).
- Follow-up filed: BL-082 (send `100 Continue` before discarding a GET/HEAD body). BL-061
  got a note on what BL-050 changed under it.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The HTTP server enforces the head timeout (408), request-head limit (431) and upload limit (413, also in place of 100 Continue), answers connection refusals with 503, and sends Server: surl, each reply recorded against pinned curl 8.21.0
