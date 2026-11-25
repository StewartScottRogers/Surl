---
id: BL-1675
title: Add SurlServerOptions.Cookies to Surl.Api
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1673]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests, Documentation/Planning/Decisions/ADR-0189-the-http-server-sets-cookies-from-set-cookie-options-and-publishes-the-cookies-curl-sends-back.md]
requirement: FR-015
created: 2026-11-24
completed: 2026-11-24
---
# BL-1675 — Add SurlServerOptions.Cookies to Surl.Api

## Goal

A test using `Surl.Api` sets cookies through a typed option and reads the cookies clients send back.

## Context

ADR-0189 decision 1 names the property `HttpServerOptions.Cookies`; `Surl.Api` has no `HttpServerOptions`, its options class is `Application/Surl.Api/SurlServerOptions.cs`. Add the list there, each entry the same text `--set-cookie` takes (as `Shares` and `Topics` are), default empty, and record the naming difference as an ADR-0189 amendment. The `Surl.Composition` wiring is BL-1673.

## Acceptance criteria

- [x] A named test in `Surl.Api.UnitTests` starts an HTTP server with one entry in `Cookies` and sees its `Set-Cookie` line.
- [x] A named test shows a bad entry refused with the `--set-cookie` text.
- [x] A named test shows that with no entry the reply carries no `Set-Cookie`.
- [x] ADR-0189 states the property's real name.
- [x] Build clean and fast tests green.

## Notes

- Plan: `SurlServerOptions.Cookies` (`IList<string>`), composed as `--set-cookie` once per item after
  `--topic-access`; a null item is an `ArgumentException`; every other refusal is `surl`'s.
- Tests in `SurlServerTests.Cookies.cs`: `StartAsync_HttpWithOneCookie_SendsItsSetCookieLine`,
  `StartAsync_ABadCookie_ThrowsWithTheExitCodeAndTextSurlWritesForSetCookie`,
  `StartAsync_HttpWithNoCookie_SendsNoSetCookie`, and
  `WaitForEventAsync_AClientSendsACookieBack_GivesItsNameAndValue` for the goal's read-back half. They failed
  before the wiring was added.
- Pipeline: planned, tested and implemented in the session, with no planner, test-writer or implementer
  delegation, because the change is one property and three lines of wiring. Review was delegated to
  `code-reviewer`; its must-fix items (`SurlEventQuery.Name` and the README said only store changes match a
  name) are fixed. Conformance skipped: no new option or byte; BL-1662 holds the pinned-curl proof.
- Docs say "HTTP/1.x and HTTP/2": HTTP/3 gets cookies only after BL-1674 and BL-1679.
- Added the ADR-0189 file to `touches` for amendment 1; no task in Doing named it.
- Verify: build 0 warnings, 28889 fast tests with 0 failures (`Surl.Api.UnitTests` 414),
  `Measure-CodeQuality.ps1 -Library Surl.Api` 0 failing members. `dotnet format --verify-no-changes` reports
  nothing in `Surl.Api`; its import-order findings elsewhere are BL-1678's.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. SurlServerOptions.Cookies sets cookies through --set-cookie from Surl.Api, and received cookies are read as CookieReceived events
