---
id: BL-1662
title: Prove upstream curl -b, -c and -j against surl's cookies in an Integration test
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1661, BL-1673]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Product/Requirements.md]
requirement: FR-015
created: 2026-11-24
completed: 2026-11-24
---
# BL-1662 — Prove upstream curl -b, -c and -j against surl's cookies in an Integration test

## Goal

Pinned upstream curl 8.21.0, run against a live `surl` with cookies configured, writes
the expected jar with `-c`, returns the cookies with `-b`, and drops session cookies with
`-j`, in a `TestCategory=Integration` test.

## Context

FR-015's proof (root `CLAUDE.md`: a behaviour is right when a pinned upstream curl build
completes the exchange). Only builds pinned in `UpstreamCurlBuilds.json` may run.

## Acceptance criteria

- [x] A test class in `Conformance/Surl.Conformance.UnitTests` runs pinned curl with `-c`
      and asserts the jar lines for each attribute the ADR names.
- [x] A second run with `-b <jar>` shows the cookies on surl's event stream.
- [x] A run with `-b <jar> -j` shows session cookies dropped and persistent ones sent.
- [x] `dotnet test --filter "TestCategory=Integration&FullyQualifiedName~Cookie"` green
      on Windows; FR-015's status in `Requirements.md` names the test.

## Notes

- Test class: `UpstreamCurlExchangesCookiesWithSurlTests` (3 tests). It starts `surl` with
  three `--set-cookie` options and `--control`, so it also runs against a published binary
  (`SURL_EXECUTABLE`), and reads `cookie_received` events from `GET /v1/events`. `Surl.Api`
  has no cookie option yet (BL-1675), and the control endpoint's `name` filter matches store
  changes only, so the test filters by `kind`.
- Measured with `Record-CurlExchange.ps1`, curl 8.21.0 (Git for Windows), 2026-11-24, adding
  to ADR-0189 a third cookie `dated=d1; Path=/app; Expires=<now+30 days>; SameSite=Strict`:
  jar lines in order `dated` (path `/app`, expiry = the `Expires` instant), `sess` (expiry 0),
  `sid` (`#HttpOnly_`, secure `TRUE`, expiry = receive time + 3600). `-b <jar>` on `/app/x`
  sent `Cookie: dated=d1; sess=xyz; sid=abc123`; `-b <jar> -j` sent
  `Cookie: dated=d1; sid=abc123`.
- `Expires` is computed from the clock (30 days ahead), not the ADR's fixed 2027 date, so the
  test does not start failing once that date passes.
- Pipeline: the change is one test class over built code, so the `/feature` agent stages were
  done in this session (no failing-first stage is possible); no new ADR, no behaviour change.
- Added `Documentation/Product/Requirements.md` to `touches` (the fourth criterion edits it);
  no task in Doing named it.
- Verified: `dotnet build -warnaserror` 0 warnings, fast tests green, the new file passes
  `dotnet format --verify-no-changes`, the Cookie `Integration` filter 3 passed on Windows.
  The rest of the `Integration` suite was not run (no existing test or production code changed).
  Linux and macOS are unrun.
- Filed BL-1680: the `--set-cookie` `--aihelp` paragraph still says the server does not set
  the cookies.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. Pinned curl 8.21.0 writes, replays and junks surl's --set-cookie cookies in UpstreamCurlExchangesCookiesWithSurlTests; FR-015 names it
