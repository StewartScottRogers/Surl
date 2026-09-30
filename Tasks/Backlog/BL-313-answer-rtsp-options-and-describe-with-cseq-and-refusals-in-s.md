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
completed:
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

- [ ] Tests in `Surl.Protocol.Rtsp.UnitTests` replay the recorded `OPTIONS` and answer it with the
      ADR's bytes, `CSeq` echoed; `DESCRIBE` answers with the ADR's body and fields; each refusal, a
      head past `--max-request-head`, the head timeout and the idle timeout answer as the ADR says;
      several requests on one connection are each answered in order; no test opens a socket.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Rtsp.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
