---
id: BL-723
title: Decide and build Range answers and PUT uploads for surl http and https
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests, Conformance/Surl.Conformance.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-05
completed: 2026-11-05
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

- [x] An ADR marked "Decided by Claude under Stewart's delegation" decides Range (single and multiple
      ranges, `If-Range`, unsatisfiable `416`) and `PUT` (`201`/`204`, `--allow-uploads`, login,
      `--max-filesize`), citing measured curl bytes.
- [x] Unit tests in `Surl.Protocol.Http.UnitTests` replay the measured bytes for each decided case.
- [x] The two `UpstreamCurlFetchesFromSurlOverHttp2Tests` tests named above assert `206` with the first
      ten bytes and `201` with the stored file, and pass on Windows.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Decided in ADR-0133 (Claude under Stewart's delegation), from curl 8.21.0 measured with
  `Record-CurlExchange.ps1` (fixtures `range-206`, `range-suffix-206`, `range-unsatisfiable-416`,
  `range-multiple-200`, `range-if-range-206`, `put-created-201`, `put-replaced-204`, each with the
  `response.bin` fed to curl).
- Range: `HttpRangeSelection` reads one `bytes` range; `FileAnswer` answers `206` with
  `Content-Range`, or `416` with `Content-Range: bytes */<length>`. Several ranges get the whole file
  `200` for now (RFC 9110 section 14.2 lets a server ignore `Range`); `multipart/byteranges` is filed
  as BL-884. `If-Range` matches only the exact `Last-Modified` (no `ETag` is sent). No
  `Accept-Ranges`, so no `200` already pinned changes.
- PUT: under `--allow-uploads` only, through a content-store upload session (as the S3 server does),
  via `HttpUploadStream`; `201` new, `204` replaced (no `Content-Length`, RFC 9110 section 8.6), `403`
  not permitted, `409` a directory or missing directory, `413` past the limit. A body-bound login
  (ADR-0045) is judged before the commit. The `405` under `--allow-uploads` now says
  `Allow: GET, HEAD, PUT`. `HttpServing` needed no change: every body already had `CopyToAsync`,
  and every writer already sent a `ContentByteRange`.
- Touches widened (rule 3; no task in Doing names them): `Conformance/Surl.Conformance.UnitTests`,
  for the two named HTTP/2 tests plus the HTTP/3 range and upload tests and the AWS SigV4 upload rows
  that pinned `405`; `Documentation/Planning/Decisions` for ADR-0133.
- Live on Windows: the HTTP/2, HTTP/3 and AWS SigV4 upload conformance tests pass with pinned curl.
  `GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback` failed once in a parallel run and passes
  alone; it does not touch Range or PUT.
- Follow-ups filed: BL-884 (multipart/byteranges), BL-885 (`--aihelp` `http` prose still says PUT is
  `405`; `Surl.Cli` is held by BL-880, so it is not edited here).
- New code in `Surl.Protocol.Http.UnitLibrary` is at 100% line and branch coverage.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. surl http and https answer curl -r with 206 (416 past the end) and store curl -T under --allow-uploads with 201/204, on HTTP/1.1, HTTP/2 and HTTP/3 (ADR-0133)
