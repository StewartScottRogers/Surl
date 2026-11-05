---
id: BL-371
title: Prove pinned upstream curl's HTTP/2 against a live surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-369, BL-370]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-371 — Prove pinned upstream curl's HTTP/2 against a live surl

## Goal

`Surl.Conformance.UnitTests` proves that pinned upstream curl completes HTTP/2 exchanges with a live
`surl`: by prior knowledge, by the `h2c` Upgrade, and by ALPN `h2` over TLS. The tests run on Windows
with the build BL-364's ADR names, and on Linux and macOS with the reference builds.

## Context

- Decision: BL-364's ADR. Its measured tables are the expected results. Its decision 1 names the
  Windows build: run it through `PinnedUpstreamCurl`'s supplementary-build helpers, as
  `UpstreamCurlOffersSshAlgorithmsTests.KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms` does.
- Shape to copy: `UpstreamCurlFetchesFromSurlOverHttp11Tests` and `UpstreamCurlFetchesFromSurlOverHttpsTests`
  (`SurlOnLoopback`, `[TestCategory("Integration")]`, inconclusive where no pinned build is
  installed). The planning name for the new class is `UpstreamCurlFetchesFromSurlOverHttp2Tests`.
- curl's own view of the version: `-w '%{http_version}'` writes `2` for HTTP/2.

## Acceptance criteria

- [x] Tests pass on Windows for `--http2-prior-knowledge` on `http://`, `--http2` on `http://` (the
      Upgrade), and default `https://`. Each asserts exit 0, the served file's bytes, and
      `%{http_version}` `2`.
- [x] Tests also cover: `-I`; a range (`-r 0-9`); `-u` with `--basic` and `--allow-plaintext-auth`
      over TLS, and `--digest`; an upload with `-T` under `--allow-uploads`; a file larger than
      65,535 bytes (flow control); and two URLs in one invocation reusing one connection (`-v` shows
      the reuse). Each asserts what the ADR measured.
- [x] ~~On a push, CI's Linux and macOS legs run the same tests green, and Notes record the run URL.~~
      Moved to BL-722 (see Notes).
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- **What was built.** `Conformance/Surl.Conformance.UnitTests/UpstreamCurlFetchesFromSurlOverHttp2Tests.cs`,
  15 `Integration` tests. On Windows every run uses the static 8.21.0 build by its SHA-256
  (`RunSupplementaryBuildAsync`, ADR-0081 decision 1); elsewhere the reference build. Passed 15/15 on
  Windows three runs in a row on 2026-11-05.
- **Measured against a live surl (2026-11-05, build S).** Prior knowledge, `--http2` Upgrade (stdout:
  the `101` head, then `HTTP/2 200`) and default `https` each exit 0 with the file and
  `%{http_version}` `2`. `-I` gives `HTTP/2 200 ` with `content-length: 16`. Basic over TLS, Basic
  over prior knowledge with `--allow-plaintext-auth`, Digest over prior knowledge and over TLS all
  exit 0 over HTTP/2; a wrong Basic password gets `401` over HTTP/2. A 262,144-byte file (four
  times the 65,535-byte initial window) arrives whole over TLS and prior knowledge. Two URLs in one
  invocation: one `* Established connection to`, then `* Reusing existing https: connection with
  host 127.0.0.1` (8.21.0's wording; not `Re-using`).
- **Range and upload pin today's answers, not the hoped-for ones (decided under rule 1).** surl's
  HTTP server answers no `Range` and refuses `PUT` with `405` on every version (checked with
  `--http1.1` too: same), so these are not HTTP/2 defects. `-r 0-9` is pinned as `200` with the
  whole file (RFC 9110 section 14.2 lets a server ignore Range); `-T` of 262,144 bytes under
  `--allow-uploads` with an account is pinned as `405`, curl exit 0, no file stored: that exercises
  ADR-0081 decision 7's `RST_STREAM NO_ERROR` for an unread body while curl is still sending past
  the initial window. Without an account the upload is `401` (ADR-0032: an upload needs a login).
  Building Range and `PUT` is filed as BL-723, which updates both tests.
- **Criterion 3 split out to BL-722**, as BL-028's was to BL-079: observing a CI run needs a pushed
  commit this lane may not make, and depending on a task that waits for this one's commits would be
  a deadlock.
- ADR-0081's Consequences ask BL-371 to measure the `413`, `FRAME_SIZE_ERROR`, `FLOW_CONTROL_ERROR`
  and idle `GOAWAY` cases; those are not in this task's criteria and stay unmeasured (BL-723 covers
  the `413` side of uploads).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Pinned upstream curl 8.21.0 completes HTTP/2 with a live surl by prior knowledge, h2c Upgrade and ALPN h2: -I, Basic, Digest, flow control, connection reuse (CI legs in BL-722)
