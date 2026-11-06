---
id: BL-385
title: Prove pinned upstream curl's HTTP/3 against a live surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-371, BL-384]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-385 — Prove pinned upstream curl's HTTP/3 against a live surl

## Goal

`Surl.Conformance.UnitTests` proves that pinned upstream curl completes HTTP/3 exchanges with a live
`surl` using `--http3-only` and `--http3`: on Windows with the build BL-364 decision 1 chose, and on
Linux and macOS with the reference builds.

## Context

- Decision: BL-372's ADR. Its measured tables and decision 6 (how surl is started for HTTP/3) give the
  expected results.
- Shape to copy: BL-371's `UpstreamCurlFetchesFromSurlOverHttp2Tests` and `SurlOnLoopback`. The
  planning name for the new class is `UpstreamCurlFetchesFromSurlOverHttp3Tests`.
- Upstream curl 8.21.0 (`docs/cmdline-opts/http3.md` and `http3-only.md`, tag `curl-8_21_0`):
  `--http3-only` fails if QUIC cannot be established, and `--http3` falls back to TCP. So the
  `--http3` test must show HTTP/3 was used (`-w '%{http_version}'` writes `3`), not merely that the
  transfer worked.
- `-k` or `--cacert` as the existing `https` tests use. ADR-0068 covers what upstream curl trusts.

## Acceptance criteria

- [x] On Windows, `--http3-only https://127.0.0.1:<port>/<file>` exits 0 with the file's bytes and
      `%{http_version}` `3`, and so does `--http3`.
- [x] Tests also cover `-I`, a range, `-u` with `--basic` and `--digest`, a `-T` upload under
      `--allow-uploads`, a 10 MiB file, and two URLs on one connection. Each asserts what the ADR
      measured.
- [x] A test with HTTP/3 turned off as the ADR allows shows `--http3-only` failing with the exit code
      upstream curl gives (measured and recorded in the ADR or in Notes) and `--http3` falling back to
      HTTP/2.
- [x] ~~On a push, CI's Linux and macOS legs run the same tests green, and Notes record the run URL.~~
      Moved to BL-768 (see Notes).
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- **What was built.** `Conformance/Surl.Conformance.UnitTests/UpstreamCurlFetchesFromSurlOverHttp3Tests.cs`,
  13 `Integration` tests against an in-process `surl --self-signed --http3 https://127.0.0.1:0/`
  (ADR-0082 decision 6). On Windows every run uses the static 8.21.0 build by its SHA-256
  (`RunSupplementaryBuildAsync`, ADR-0082 decision 1); elsewhere the reference build. Passed 13/13 on
  Windows three runs in a row on 2026-11-05.
- **Measured against a live surl (2026-11-05, build S).** `--http3-only` and `--http3` each exit 0
  with the file and `%{http_version}` `3`, so `--http3` really used QUIC, not the TCP fallback. `-I`
  gives `HTTP/3 200 ` with `content-length: 16` and no `alt-svc`; the same `-I` over TCP gives
  `HTTP/2 200 ` with `alt-svc: h3=":<port>"; ma=86400`. Basic and Digest exit 0 over HTTP/3; a wrong
  Basic password gets `401` over HTTP/3. A 10 MiB file arrives whole over HTTP/3. Two URLs in one
  invocation: `* Reusing existing https: connection with host 127.0.0.1`.
- **Range and upload pin today's answers (rule 1), as BL-371 did.** surl answers no `Range` and
  refuses `PUT` with `405` on every version (BL-723 builds both and updates these tests): `-r 0-9` is
  `200` with the whole file; `-T` of 262,144 bytes under `--allow-uploads` with an account is `405`,
  curl exit 0, no file stored, so curl accepts the server's `STOP_SENDING H3_NO_ERROR` for the unread
  body (ADR-0082 Amendment 1, point 3).
- **HTTP/3 off** (surl without `--http3`, ADR-0082 decision 6): `--http3-only` exits **56** on Windows
  (`Failed to connect to 127.0.0.1`: the ICMP port-unreachable is reported as a receive failure), as
  ADR-0082's "Measured" table recorded against no UDP listener; off Windows the test expects that
  table's Linux exit 7. `--http3` falls back and is served over HTTP/2 (`%{http_version}` `2`).
- **CI criterion moved to BL-768**, as BL-371's was to BL-724: observing a CI run needs a pushed
  commit this lane may not make, and depending on a task that waits for this one's commits would
  deadlock.
- ADR-0082's Consequences also ask BL-385 to measure curl past each decision 7 limit,
  `H3_VERSION_FALLBACK` and `Alt-Svc` with `--alt-svc`. None is in this task's criteria, and
  `H3_VERSION_FALLBACK` is not built yet (Amendment 1, point 7), so they stay unmeasured.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Pinned upstream curl 8.21.0 completes HTTP/3 with a live surl --http3 via --http3-only and --http3: -I, Basic, Digest, 10 MiB, connection reuse, fallback to HTTP/2 when off (CI legs in BL-768)
