---
id: BL-888
title: Answer several ranges with multipart/byteranges in surl http and https
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-723, BL-1035]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-888 — Answer several ranges with multipart/byteranges in surl http and https

## Goal

`surl http://` and `surl https://` answer curl's `-r 0-1,3-4` with `206 Partial Content` and a
`multipart/byteranges` body (RFC 9110, section 14.6) on HTTP/1.x, HTTP/2 and HTTP/3, where today
they serve the whole file with `200`.

## Context

- Found in BL-723 (2026-11-05). ADR-0133 decision 1 answers one range `206`, and ignores several
  (`HttpRangeSelection` returns null for a field with a comma), as RFC 9110 section 14.2 lets a
  server. Pinned upstream curl 8.21.0 sends `Range: bytes=0-1,3-4` for `-r 0-1,3-4`
  (`Protocols/Http/Surl.Protocol.Http.UnitTests/Fixtures/range-multiple-200`) and writes whatever body comes.
- `HttpAnswerBody.ContentFile` carries one `ContentByteRange`; several parts need a new
  `HttpAnswerBody` kind that `Http1AnswerWriter`, `Http2StreamAnswerWriter` and
  `Http3StreamAnswerWriter` each write.
- Measure curl first with `Record-CurlExchange.ps1` and a canned `multipart/byteranges` answer.

## Acceptance criteria

- [ ] ADR-0133 has an amendment deciding the boundary, part fields, coalescing of overlapping
      ranges and a cap on the number of ranges, citing measured curl bytes.
- [ ] Unit tests in `Surl.Protocol.Http.UnitTests` replay the measured request and pin the
      `206` `multipart/byteranges` answer byte for byte, over HTTP/1.1, HTTP/2 and HTTP/3.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-11 (lane 4): measured pinned upstream curl 8.21.0 (win-x64 reference) with
  `Record-CurlExchange.ps1`, fixtures left uncommitted for the shift to stash:
  `Fixtures/range-multiple-206` (`-r 0-1,3-4`, canned `206` `multipart/byteranges` with boundary
  `SURL_BYTERANGES`, 181-byte body; curl writes the multipart body verbatim to stdout, exits 0) and
  `Fixtures/range-overlapping-206` (`-r 0-4,2-6`, canned single-part `206 bytes 0-6/17`; curl writes
  `Hello f`, exits 0).
- Choices taken (to go into the ADR-0133 amendment): fixed boundary `SURL_BYTERANGES` (deterministic
  bytes every run); each part carries the file's `Content-Type` then `Content-Range`; ranges naming no
  byte are dropped, all dropped is `416`; ranges that overlap or touch are coalesced and sorted, and a
  set that coalesces to one is a plain single-part `206`; otherwise parts keep request order (RFC 9110
  14.6); more than 100 ranges, or any malformed range, ignores the field (`200` whole file).
- Code written but not built (uncommitted): `HttpAnswerBody.ContentFileParts` + `ContentFilePart`
  in `Surl.HttpServing` (writers copy preambles, ranges, epilogue; `ContentFileParts.Of(ContentFile)`
  makes every writer use one copy path), `Http1AnswerWriter`, `Http2StreamAnswerWriter`,
  `Http3StreamAnswerWriter`, `HttpRangeSelection` (returns a list; empty = 416),
  `HttpByteRangesBody`, `HttpRequestAnswerer.ByteRangesAnswer`. Still to do: tests (HTTP/1.1 fixture
  replay, HTTP/2 and HTTP/3 multipart answers, `HttpRangeSelectionTests` for the list result),
  Fixtures/README rows, the ADR-0133 amendment, the project CLAUDE.md line on ranges.
- Returned to Backlog behind BL-1035: `dotnet build` fails, the pinned .NET SDK 10.0.401 is not
  installed (installed: 10.0.100-rc.1, 11.0.100-rc.2, 11.0.100).

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build fails
