---
id: BL-1446
title: Measure what pinned upstream curl 8.21.0 sends and does for -z, --etag-compare and --etag-save against 200, 304 and 412 answers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitTests/Fixtures]
requirement: none
created: 2026-11-20
completed:
---
# BL-1446 — Measure what pinned upstream curl 8.21.0 sends and does for -z, --etag-compare and --etag-save against 200, 304 and 412 answers

## Goal

Recordings under `Protocols/Http/Surl.Protocol.Http.UnitTests/Fixtures` show the request bytes,
stdout, stderr, exit code and written files of pinned upstream curl 8.21.0 for `-z`/`--time-cond`,
`--etag-compare` and `--etag-save`, against canned 200, 304 and 412 answers, so BL-1447 decides from
measurements.

## Context

- Surly bug report `2026-11-19T1942-Surl-bug-report.md` item 12 (an ask): no `If-Modified-Since`, no
  ETags (only `If-Range` by date), no index page.
- Today: the HTTP server sends `Last-Modified` (`Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpRequestAnswerer.cs:310`)
  and honours `If-Range` only as an exact `Last-Modified` (`:336-341`; ADR-0133 says "the server sends
  no `ETag`, so an entity-tag `If-Range` never matches"). Nothing reads `If-Modified-Since`,
  `If-Unmodified-Since`, `If-None-Match` or `If-Match`.
- Upstream curl 8.21.0 options that depend on them (its own `--manual`): `-z, --time-cond <time>`
  "(HTTP FTP) Request a file that has been modified later than the given time and date, or one that
  has been modified before that time"; a leading `-` asks for older; a non-date is a file whose mtime
  is used. `--etag-compare <file>` "Make a conditional HTTP request for the specific ETag read from the
  given file by sending a custom If-None-Match header". `--etag-save <file>` "Save an HTTP ETag to the
  specified file ... If no ETag is sent by the server, an empty file is created." FTP `-z` is answered
  from `MDTM`, which the FTP server already serves (ADR-0052), so FTP needs nothing.
- Measure with `Record-CurlExchange.ps1` (`-Response` sets the canned answer) and only the pinned
  win-x64 reference build (`UpstreamCurlBuilds.json`). Never the Curl port.

## Acceptance criteria

- [ ] Recorded cases, each a fixture folder with request bytes, stdout, stderr and exit code:
      `-z "<date>"` against 304 and against 200 with a `Last-Modified` older than the date;
      `-z "-<date>"` against 412 and against 200; `-z <file>` (mtime); `--etag-compare` with
      `"abc"` in the file against 304; `--etag-save` against a 200 with `ETag: "abc"` and with none;
      both options on one file.
- [ ] Each folder records the exact `If-Modified-Since`, `If-Unmodified-Since` or `If-None-Match` line
      curl sent, and for `--etag-save` the saved file's bytes.
- [ ] The cases are run with `--fail` too where the answer is 412, recording the exit code.
- [ ] `Protocols/Http/Surl.Protocol.Http.UnitTests/Fixtures/README.md` lists each case, the command line,
      the curl build's SHA-256 and the date measured.
- [ ] No test needs `TestCategory=Integration`, and the fast tests stay green.

## Notes

- The index page part of the ask (a directory answered with `index.html`) is not filed: no upstream
  curl option depends on it, and ADR-0008 answers a directory `404`.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` item 12.
