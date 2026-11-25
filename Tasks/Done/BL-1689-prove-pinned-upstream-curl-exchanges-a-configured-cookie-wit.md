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
completed: 2026-11-25
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

- [x] A named `Integration` test runs the pinned HTTP/3 build with `--http3-only -c <jar>` against `surl --http3 --set-cookie` and finds the cookie in the jar.
- [x] A named `Integration` test runs it again with `-b <jar>` and finds a `CookieReceivedEvent` or the `cookie` field in curl's verbose output.
- [x] FR-015's status names the new tests and is true of what they run.
- [x] Build clean and fast tests green.

## Notes

- Tests added to `UpstreamCurlExchangesCookiesWithSurlTests`: `CookieJar_Http3Only_HoldsSurlsSetCookie` and
  `CookieReplay_FromTheJarWithHttp3Only_PublishesTheCookieOnSurlsEventStream`.
- Chose the `cookie_received` event, read through the control endpoint, over curl's verbose output: it shows what surl
  received. The control endpoint is read with the reference build; the HTTP/3 exchange uses the static 8.21.0 build on
  Windows and the reference build elsewhere, as `UpstreamCurlFetchesFromSurlOverHttp3Tests` does. Each HTTP/3 run also
  checks `%{http_version}` is `3`.
- Windows, 2026-11-25: the class's 5 tests passed; `dotnet build -warnaserror` 0 warnings; fast tests 28944 passed,
  0 failed, 3 skipped. Not run on Linux or macOS.
- `dotnet format --verify-no-changes` is clean on the changed file. It reports end-of-line and import-order errors in
  files this task did not change (Smtp, Composition, Control, Networking, Ssh tests, McpServer, Ldap); left as found.
- Filed BL-1690 (cookies over `https` HTTP/1.1, HTTP/2 and h2c) and BL-1691 (stale `Surl.Cookies` line in the roadmap).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. Pinned upstream curl stores surl's --set-cookie cookie over HTTP/3 and sends it back, proven by two Integration tests
