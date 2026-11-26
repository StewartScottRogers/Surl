---
id: BL-1749
title: Withhold SASL initial responses from the exchange log when words are separated by tabs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Documentation/Planning/Decisions/ADR-0183-a-sasl-line-redaction-rule-withholds-initial-responses-and-continuation-answers.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1749 — Withhold SASL initial responses from the exchange log when words are separated by tabs

## Goal

An `AUTH` or `AUTHENTICATE` line whose words are separated by tabs is logged with the initial response as `****`.

## Context

- Found while working BL-1748. `SaslLineRedactionRule.SplitOnSpaceRuns` splits on spaces only, so
  `A1<tab>AUTHENTICATE<tab>PLAIN<tab><response>` reads as one word and the response is logged. Read from the code; no
  test reproduces it yet.
- The SMTP, IMAP and POP3 servers share the rule. BL-1748 amended ADR-0183 (amendment 3) the same way for IMAP `LOGIN`.

## Acceptance criteria

- [ ] A named test in `Surl.LineProtocol.UnitTests` shows the initial response masked on an `AUTH` line separated by tabs.
- [ ] A named test shows it masked on a tagged `AUTHENTICATE` line separated by tabs.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
