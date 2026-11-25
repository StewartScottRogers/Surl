---
id: BL-1689
title: Prove pinned upstream curl exchanges a configured cookie with surl over HTTP/3
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Product/Requirements.md]
requirement: FR-015
created: 2026-11-25
completed:
---
# BL-1689 — Prove pinned upstream curl exchanges a configured cookie with surl over HTTP/3

## Goal

A conformance test shows pinned upstream curl storing a `--set-cookie` cookie from `surl --http3` and sending it back.

## Context

FR-015, ADR-0189. `UpstreamCurlExchangesCookiesWithSurlTests` runs over `http` with HTTP/1.1 only. BL-1679 checked HTTP/3
by hand: static-curl 8.21.0 (Windows, ADR-0082 decision 1) with `--http3-only -k -c`, then `-b`, against
`surl --allow-anonymous --self-signed --http3 --set-cookie "session=t1;Path=/;HttpOnly" https://127.0.0.1:0/` wrote the
cookie to the jar and sent `cookie: session=t1`. `UpstreamCurlFetchesFromSurlOverHttp3Tests` shows how the HTTP/3 build
is run.

## Acceptance criteria

- [ ] A named `Integration` test runs the pinned HTTP/3 build with `--http3-only -c <jar>` against `surl --http3 --set-cookie` and finds the cookie in the jar.
- [ ] A named `Integration` test runs it again with `-b <jar>` and finds a `CookieReceivedEvent` or the `cookie` field in curl's verbose output.
- [ ] FR-015's status names the new tests and is true of what they run.
- [ ] Build clean and fast tests green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
