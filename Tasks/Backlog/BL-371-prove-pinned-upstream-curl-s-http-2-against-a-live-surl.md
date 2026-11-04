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
completed:
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

- [ ] Tests pass on Windows for `--http2-prior-knowledge` on `http://`, `--http2` on `http://` (the
      Upgrade), and default `https://`. Each asserts exit 0, the served file's bytes, and
      `%{http_version}` `2`.
- [ ] Tests also cover: `-I`; a range (`-r 0-9`); `-u` with `--basic` and `--allow-plaintext-auth`
      over TLS, and `--digest`; an upload with `-T` under `--allow-uploads`; a file larger than
      65,535 bytes (flow control); and two URLs in one invocation reusing one connection (`-v` shows
      the reuse). Each asserts what the ADR measured.
- [ ] On a push, CI's Linux and macOS legs run the same tests green, and Notes record the run URL.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
