---
id: BL-1754
title: Withhold a SASL initial response on a line whose tag is itself AUTH or AUTHENTICATE
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Documentation/Planning/Decisions/ADR-0183-a-sasl-line-redaction-rule-withholds-initial-responses-and-continuation-answers.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1754 — Withhold a SASL initial response on a line whose tag is itself AUTH or AUTHENTICATE

## Goal

A tagged `AUTHENTICATE` line whose tag is also a SASL command word is logged with the initial response as `****`.

## Context

- Found while working BL-1749. `SaslLineRedactionRule.TryFindInitialResponse` tries the first word as the command
  before the second, so on `AUTHENTICATE AUTHENTICATE PLAIN <response>` (or tag `AUTH` in the SOCKS tunnel's rule) it
  masks the mechanism and logs the response. Read from the code; no test reproduces it yet.
- ADR-0183 amendment 3, point 2, settled the same ambiguity for `LOGIN LOGIN` by withholding both readings.

## Acceptance criteria

- [ ] A named test in `Surl.LineProtocol.UnitTests` shows the response masked on
      `AUTHENTICATE AUTHENTICATE PLAIN <response>`.
- [ ] ADR-0183 gains an amendment that states the rule.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
