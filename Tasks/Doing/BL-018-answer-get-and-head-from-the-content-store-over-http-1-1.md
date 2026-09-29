---
id: BL-018
title: Answer GET and HEAD from the content store over HTTP/1.1
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-009, BL-017]
touches: [Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-018 — Answer GET and HEAD from the content store over HTTP/1.1

## Goal

`Surl.Protocol.Http.UnitLibrary` contains an HTTP protocol server that implements the
protocol-server interface from Abstractions for the `http` scheme. It answers GET and
HEAD for files in the content store over HTTP/1.0 and HTTP/1.1, with persistent
connections for 1.1. Each response it sends has been fed to the pinned upstream curl
8.21.0 build, which accepted it.

## Context

- Builds on BL-017's request-head parser and fixtures (in
  `Surl.Protocol.Http.UnitTests/Fixtures/`), and on the content store from BL-008 and
  BL-009. Add a `ProjectReference` to `Surl.Content.UnitLibrary`, which ADR-0002 allows,
  and nothing else.
- The protocol-server interface and exchange context come from the listener-seam ADR
  recorded by BL-000 (types added by BL-005). Report exchange events through the
  context as that ADR says.
- Semantics: RFC 9110 (status codes, `Content-Length`, HEAD) and RFC 9112 (persistence,
  section 9.3; HTTP/1.0 closes unless keep-alive). Time for the `Date` header comes from
  the exchange context's `TimeProvider`.
- Decide in the `/feature` plan, and state in the server's XML doc: the status for a
  refused path, a missing path and a directory (listing is a later task); the status
  for an unsupported method (RFC 9110 says 501, with 405 plus `Allow` for a known method
  the resource refuses); the status for a request-head parse failure from BL-017; and
  the `Content-Type` sent before media types exist (a later `Surl.Content` task).
- Proving a response acceptable before `surl` is wired (BL-019): run
  `Record-CurlExchange.ps1` with the exact response bytes the test expects as
  `-Response`, using the pinned build, and check curl's `exitcode.txt` and `stdout.bin`.
  Commit each recording under `Surl.Protocol.Http.UnitTests/Fixtures/<case-name>/`,
  embedded as resources, with the command line in that folder's `README.md`.
- `--fail` makes curl exit 22 (`CURLE_HTTP_RETURNED_ERROR`) on a status of 400 or
  above (https://curl.se/libcurl/c/libcurl-errors.html, checked 2026-09-28).

## Acceptance criteria

- [ ] The HTTP server class declares the `http` scheme as the ADR's interface requires.
      `ProtocolIsolationTests` pass with the new `Surl.Content.UnitLibrary` reference.
- [ ] Fast tests replay BL-017's GET, HEAD and HTTP/1.0 fixtures through the in-memory
      connection against an in-memory content store, and assert the exact response
      bytes: status line, `Date` from a fixed `TimeProvider`, `Content-Length`, the
      decided `Content-Type`, and the body (none for HEAD).
- [ ] A fast test replays the two-request fixture and proves both responses go out on
      one HTTP/1.1 connection. Another proves HTTP/1.0 closes after its response.
- [ ] Fast tests cover a missing file, a refused path (`/%2e%2e/x`), a directory, an
      unsupported method and a malformed head, each asserting the decided status and
      that the connection is closed or kept as decided.
- [ ] For the 200 GET, 200 HEAD (`-I`) and 404 (with `--fail`) cases, the pinned
      upstream curl 8.21.0 build was fed the exact expected response with
      `Record-CurlExchange.ps1 -Response`. The committed recordings show exit code 0
      with `stdout.bin` equal to the file body for GET, 0 for HEAD, and 22 for the 404
      case.
- [ ] `dotnet build Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, the fast
      tests are green with no `Integration` test in `Surl.Protocol.Http.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Http.UnitLibrary`.

## Notes

This task uses the `feature` pipeline, not `protocol`, because the live conformance
check against a running `surl` needs BL-019 and is done by BL-020. Ranges, conditional
requests, uploads, redirects, authentication and cookies are follow-up tasks.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
