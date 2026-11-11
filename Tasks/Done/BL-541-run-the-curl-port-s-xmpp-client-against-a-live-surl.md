---
id: BL-541
title: Run the Curl port's xmpp client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-540, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-541 â€” Run the Curl port's xmpp client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl xmpp://` (STARTTLS) and `xmpps://`: log
in with SCRAM-SHA-256 and send a message, ask for service discovery, and have a bad password refused. Each
assertion comes from RFC 6120 and XEP-0030, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help xmpp` and its cases in `Curl.Conformance.Extended.UnitTests/Xmpp/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [x] The test class exists with the three exchanges over both schemes, each passing on Windows with the pinned
      client present.
- [x] `Documentation/Conformance/Extended/Xmpp.md`: every row is covered by a named test or marked `n/a` with a reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Pinned Curl port xmpp client logs in (SCRAM), sends, discovers and is refused on a live surl over xmpp and xmpps; Xmpp.md rows name tests
