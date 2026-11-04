---
id: BL-385
title: Prove pinned upstream curl's HTTP/3 against a live surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-371, BL-384]
touches: [Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

- [ ] On Windows, `--http3-only https://127.0.0.1:<port>/<file>` exits 0 with the file's bytes and
      `%{http_version}` `3`, and so does `--http3`.
- [ ] Tests also cover `-I`, a range, `-u` with `--basic` and `--digest`, a `-T` upload under
      `--allow-uploads`, a 10 MiB file, and two URLs on one connection. Each asserts what the ADR
      measured.
- [ ] A test with HTTP/3 turned off as the ADR allows shows `--http3-only` failing with the exit code
      upstream curl gives (measured and recorded in the ADR or in Notes) and `--http3` falling back to
      HTTP/2.
- [ ] On a push, CI's Linux and macOS legs run the same tests green, and Notes record the run URL.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
