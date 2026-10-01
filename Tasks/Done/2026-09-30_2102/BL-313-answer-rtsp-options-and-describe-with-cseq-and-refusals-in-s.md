---
id: BL-313
title: Answer RTSP OPTIONS and DESCRIBE with CSeq and refusals in Surl.Protocol.Rtsp
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-286, BL-293]
touches: [Surl.Protocol.Rtsp.UnitLibrary, Surl.Protocol.Rtsp.UnitTests]
requirement: FR-051
created: 2026-09-30
completed: 2026-09-30
---
# BL-313 — Answer RTSP OPTIONS and DESCRIBE with CSeq and refusals in Surl.Protocol.Rtsp

## Goal

`RtspProtocolServer` in `Surl.Protocol.Rtsp` implements `IConnectionProtocolServer` for `rtsp`, reads
RTSP/1.0 requests through `Surl.HttpMessage`, answers `OPTIONS` and `DESCRIBE` with `CSeq` echoed as
BL-286's ADR decides, and answers every other request, malformed request and limit with the ADR's
status, replaying request bytes recorded from the pinned build.

## Context

- Decisions: BL-286's ADR (the response head and its fields, `Public` for `OPTIONS`, what `DESCRIBE`
  answers and from where, the refusal status for each case - unknown method, missing `CSeq`, bad
  version, a path the content store has not - the limits and their answers, the verbose notes);
  BL-281's ADR (the library the head is read and written with); ADR-0006 (`--max-request-head`, the
  head timeout, NFR-013); ADR-0019 (`Server: surl`, the refusal deadline).
- Code: BL-293's types in `Surl.HttpMessage.UnitLibrary`, read with protocol `RTSP/1.0`; the csproj
  gains the `Surl.HttpMessage.UnitLibrary` reference and, if `DESCRIBE` serves content-store files,
  `Surl.Content.UnitLibrary` (both allowed by ADR-0002's table as amended by BL-281's ADR). The HTTP
  server is the pattern for a persistent request loop, not a reference (ADR-0002 decision 3).
- Requests other than `OPTIONS` and `DESCRIBE` are answered by later tasks (BL-314 to BL-316); until
  then they get the ADR's "not implemented" answer so the task stands alone.
- Fixtures: the `OPTIONS` requests of BL-286's cases recorded with `Record-CurlExchange.ps1` against
  the Windows reference build; `DESCRIBE` from libcurl if BL-285's or BL-286's ADR pinned
  `libcurl-4.dll` and it was recorded by then, otherwise from RFC 2326's section 14 examples.

## Acceptance criteria

- [x] Tests in `Surl.Protocol.Rtsp.UnitTests` replay the recorded `OPTIONS` and answer it with the
      ADR's bytes, `CSeq` echoed; `DESCRIBE` answers with the ADR's body and fields; each refusal, a
      head past `--max-request-head`, the head timeout and the idle timeout answer as the ADR says;
      several requests on one connection are each answered in order; no test opens a socket.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Rtsp.UnitLibrary`.

## Notes

- Built ADR-0074 decisions 1 to 4 and decision 8's head limits: `RtspProtocolServer`,
  `RtspRequestResponder`, `RtspSessionDescription`, `RtspStatus`, `RtspUnreadRequestDrainer`. The
  csproj references `Surl.HttpMessage.UnitLibrary` and `Surl.Content.UnitLibrary` (ADR-0002's table
  as ADR-0070 amends it). Not registered in `Surl.Console`: BL-317 adds the help and topic.
- Fixtures (2026-09-30, pinned win-x64 curl 8.21.0, `Record-CurlExchange.ps1 -Raw`): `options-plain`
  and `options-fields` (exit 0, stdout the head, `-v` "left intact"), `not-implemented-501` (exit
  22 with `-f`), `bad-request-400` (exit 0, `-w` prints 400). Each `response.bin` is surl's exact
  answer, fed to curl before any test pinned it. `DESCRIBE` uses RFC 2326 section 10.2's example
  (`DESCRIBE rtsp://server.example.com/fizzle/foo`, `CSeq: 312`) until BL-333 records libcurl's.
- Choices where ADR-0074 was silent (sensible defaults, no ADR needed):
  - A request with no usable `CSeq` that announced a body is `400` **closing**, not kept open:
    the body is never read (decision 2 reads bodies only for checks 4 to 7), so the next head
    could not be found.
  - A body that ends before its `Content-Length` is `400` with the `CSeq`, closing, as the HTTP
    server answers one.
  - `ANNOUNCE`, `SETUP`, `PLAY`, `PAUSE`, `TEARDOWN`, `GET_PARAMETER`, `SET_PARAMETER`, `RECORD` are
    `501 Not Implemented` until BL-314 to BL-316, as the task's Context says. A `Session` on
    `OPTIONS` is not checked yet (BL-315), and no login is judged yet (BL-314, decision 7).
  - The connection-limit `503` carries `Server: surl` only, no `Date`: `IConnectionRefusalWriter`
    has no clock, and the HTTP server's `503` has none either.
  - The SDP origin is the connection's local address; an IPv4-mapped IPv6 address is written as
    IPv4, an IPv6 scope ID is dropped (SDP has no `%`), and an endpoint with no IP address is
    `IP4 0.0.0.0`. The `s=` name is the mapped file's name.
  - The idle timeout and other exchange limits arrive as the exchange's cancellation and end it
    with no bytes, as in the HTTP server.
- Verified: `dotnet build -warnaserror` clean; fast tests green (Rtsp 82 passed);
  `Measure-CodeQuality.ps1 -Library Surl.Protocol.Rtsp.UnitLibrary`: line 100, branch 100,
  34 members, 0 failing, worst CRAP 8.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Protocol.Rtsp answers OPTIONS and DESCRIBE with CSeq echoed, and every ADR-0074 refusal and head limit, at 100% coverage
