---
id: BL-1690
title: Prove pinned upstream curl exchanges cookies with surl over https, HTTP/2 and h2c
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Product/Requirements.md]
requirement: FR-015
created: 2026-11-25
completed: 2026-11-25
---
# BL-1690 — Prove pinned upstream curl exchanges cookies with surl over https, HTTP/2 and h2c

## Goal

Conformance tests show pinned upstream curl storing a `--set-cookie` cookie from `surl` and sending it back over
`https` with HTTP/1.1, over `https` with HTTP/2 and over h2c.

## Context

FR-015, ADR-0189. `UpstreamCurlExchangesCookiesWithSurlTests` covers `http` with HTTP/1.1 and `https` with
`--http3-only` (BL-1689). `UpstreamCurlFetchesFromSurlOverHttp2Tests` shows which build runs HTTP/2 on each platform.

## Acceptance criteria

- [x] Named `Integration` tests run pinned upstream curl with `-c` and then `-b` over `https` with `--http1.1`, over `https` with `--http2` and over `--http2-prior-knowledge`, each checking the HTTP version curl spoke and the `cookie_received` event.
- [x] FR-015's status names the new tests and is true of what they run.
- [x] Build clean and fast tests green.

## Notes

- Three tests added to `UpstreamCurlExchangesCookiesWithSurlTests`: `CookieExchange_HttpsWithHttp11_...`,
  `CookieExchange_HttpsWithHttp2_...` and `CookieExchange_H2cWithPriorKnowledge_...`. The HTTP/3 tests now
  share their fetch helper.
- Builds: `--http1.1` runs the reference build; `--http2` and `--http2-prior-knowledge` run the static
  8.21.0 build on Windows (ADR-0081 decision 1). No new byte was pinned: the jar line is the one the
  HTTP/3 test already expects.
- 2026-11-25, Windows: all 8 tests of the class pass; `dotnet build -warnaserror` has 0 warnings; every
  fast test project passes. Only this class of the `Integration` tests was run.
- `dotnet format --verify-no-changes` passes for the changed `.cs` file and fails on files this task did
  not touch; filed as BL-1692. No production code changed, so no ADR.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. Upstream curl stores and sends back surl's cookie over https with HTTP/1.1 and HTTP/2 and over h2c
