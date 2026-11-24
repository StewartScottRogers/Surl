---
id: BL-1650
title: Decide how the HTTP server sets cookies and records the ones upstream curl sends back
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: FR-015
created: 2026-11-24
completed: 2026-11-24
---
# BL-1650 — Decide how the HTTP server sets cookies and records the ones upstream curl sends back

## Goal

An ADR decides how `surl` sets cookies on HTTP replies and records the `Cookie` headers
upstream curl sends back, so BL-1659 to BL-1662 can build FR-015 without a question.

## Context

`Foundation/Surl.Cookies.UnitLibrary` holds no source file and
`Foundation/Surl.Cookies.UnitTests` holds no test, yet its `CLAUDE.md`, FR-015, the product
overview ("Also in scope", "Cookies") and the Phase 1 row all say the HTTP server sets
cookies through it. The roadmap's Milestone 1 status records it as the one Phase 1 piece
with no code. Found by the align-and-document audit of 2026-11-24 and the coverage audit
of the same day.

Decision taken in this audit, by the standing rules (root `CLAUDE.md`, "Decisions": a
complete server-side mate leaves nothing out because it is hard): **build it, do not
retire it.** Upstream curl's `-b`/`--cookie`, `-c`/`--cookie-jar` and `-j`/
`--junk-session-cookies` are only exercisable against a server that sends `Set-Cookie`
with every attribute curl's cookie parser reads and that lets a test see what came back.
As a stand-in, a test needs to seed a session cookie and assert the client returned it.
Retiring the project would leave FR-015 unanswerable and the product overview untrue.

Upstream: https://curl.se/docs/http-cookies.html and https://curl.se/docs/manpage.html
(`-b`, `-c`, `-j`), checked against curl 8.21.0.

## Acceptance criteria

- [x] `Documentation/Planning/Decisions/ADR-####-<slug>.md` exists in ADR-0001's shape,
      marked "Decided by Claude under Stewart's delegation", and decides: the option(s)
      that configure cookies (name, argument shape, per listener or per path), which
      attributes are written (`Domain`, `Path`, `Expires`, `Max-Age`, `Secure`,
      `HttpOnly`, `SameSite`) and how, how received `Cookie` headers reach the event stream
      and `Surl.Api`, and that cookies stay off until asked for (secure by default).
- [x] The ADR states the `Set-Cookie` bytes and curl's resulting jar line for at least one
      case, measured with `Record-CurlExchange.ps1` against the pinned 8.21.0 build.
- [x] The ADR states whether ADR-0124 decision 6 ("cookies are never seeded") still holds
      for server-set cookies, and amends ADR-0124 if not.
- [x] FR-015 in `Documentation/Product/Requirements.md` cites the new ADR.
- [x] The ADR's index row is added to `Documentation/Planning/Decisions/README.md`.

## Notes

ADR-0189 holds the decision (curl 8.21.0 measured: Set-Cookie bytes, jar lines, -b replay). ADR-0124 decision 6 is about generated secrets, so no amendment. Scoping keys `on-path`, `on-listener` chosen so they never clash with the cookie `Path` attribute. BL-1659 (library), BL-1660 (HTTP server), BL-1661 (options and `--aihelp`) and BL-1662
(pinned curl proof) depend on this.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. ADR-0189 decides --set-cookie, attributes, CookieReceivedEvent; measured with curl 8.21.0; FR-015 and ADR index updated
