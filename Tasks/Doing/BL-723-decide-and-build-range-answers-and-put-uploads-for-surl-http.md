---
id: BL-723
title: Decide and build Range answers and PUT uploads for surl http and https
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-723 — Decide and build Range answers and PUT uploads for surl http and https

## Goal

`surl http://` and `surl https://` answer curl's `-r` with `206 Partial Content` and the requested
bytes, and store a `curl -T` upload under `--allow-uploads` (with a login, ADR-0032), on HTTP/1.x and
HTTP/2 alike, as an ADR decides from measured upstream curl 8.21.0.

## Context

- Found in BL-371 (2026-11-05): against a live surl, pinned upstream curl's `-r 0-9` is answered
  `200` with the whole file, and `-T` under `--allow-uploads` with an account is answered `405`
  (`HttpRequestAnswerer.MethodsRefusedWithAllow` lists `PUT`), on HTTP/1.1 and HTTP/2 alike.
  `HttpAnswerBody.ContentFile` already carries a `ContentByteRange`, so a range answer has a seam.
- `UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithRange_HttpsByAlpn_IsServedTheWholeFileWith200OverHttp2`
  and `Upload_AllowUploads_IsRefusedWith405AndCurlExitsZeroOverHttp2` pin today's answers; this task
  changes them to the new ones in the same diff.
- ADR-0081 decisions 5 and 7 already say how an HTTP/2 upload is bounded (`--max-filesize`, `413`
  then `RST_STREAM NO_ERROR`).
- Measure curl first with `Record-CurlExchange.ps1` (RFC 9110 sections 14 and 9.3.4).

## Acceptance criteria

- [ ] An ADR marked "Decided by Claude under Stewart's delegation" decides Range (single and multiple
      ranges, `If-Range`, unsatisfiable `416`) and `PUT` (`201`/`204`, `--allow-uploads`, login,
      `--max-filesize`), citing measured curl bytes.
- [ ] Unit tests in `Surl.Protocol.Http.UnitTests` replay the measured bytes for each decided case.
- [ ] The two `UpstreamCurlFetchesFromSurlOverHttp2Tests` tests named above assert `206` with the first
      ten bytes and `201` with the stored file, and pass on Windows.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
