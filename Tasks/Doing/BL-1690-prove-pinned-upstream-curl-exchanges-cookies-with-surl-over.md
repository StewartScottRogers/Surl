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
completed:
---
# BL-1690 — Prove pinned upstream curl exchanges cookies with surl over https, HTTP/2 and h2c

## Goal

Conformance tests show pinned upstream curl storing a `--set-cookie` cookie from `surl` and sending it back over
`https` with HTTP/1.1, over `https` with HTTP/2 and over h2c.

## Context

FR-015, ADR-0189. `UpstreamCurlExchangesCookiesWithSurlTests` covers `http` with HTTP/1.1 and `https` with
`--http3-only` (BL-1689). `UpstreamCurlFetchesFromSurlOverHttp2Tests` shows which build runs HTTP/2 on each platform.

## Acceptance criteria

- [ ] Named `Integration` tests run pinned upstream curl with `-c` and then `-b` over `https` with `--http1.1`, over `https` with `--http2` and over `--http2-prior-knowledge`, each checking the HTTP version curl spoke and the `cookie_received` event.
- [ ] FR-015's status names the new tests and is true of what they run.
- [ ] Build clean and fast tests green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
