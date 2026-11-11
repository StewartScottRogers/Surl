---
id: BL-888
title: Answer several ranges with multipart/byteranges in surl http and https
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-723]
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

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
