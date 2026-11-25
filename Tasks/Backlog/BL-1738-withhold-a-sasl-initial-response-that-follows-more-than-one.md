---
id: BL-1738
title: Withhold a SASL initial response that follows more than one space in SaslLineRedactionRule
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1738 — Withhold a SASL initial response that follows more than one space in SaslLineRedactionRule

## Goal

`AUTH PLAIN  <base64>`, with more than one space before the response (or the mechanism), is logged with the response
as `****`.

## Context

- Found in the code review of BL-1710. `SaslLineRedactionRule.TryFindInitialResponse` splits a line on single spaces,
  so a doubled space makes the third token empty and the response is logged. The SMTP, IMAP and POP3 servers and the
  SOCKS tunnel (ADR-0182 Amendment 9) all use this rule. curl sends single spaces.

## Acceptance criteria

- [ ] Named tests in `Surl.LineProtocol.UnitTests` show the response masked after two spaces before the mechanism and
      before the response, with and without an IMAP tag.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
