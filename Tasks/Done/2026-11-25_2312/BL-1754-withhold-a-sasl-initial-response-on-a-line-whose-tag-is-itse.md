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
completed: 2026-11-25
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

- [x] A named test in `Surl.LineProtocol.UnitTests` shows the response masked on
      `AUTHENTICATE AUTHENTICATE PLAIN <response>`.
- [x] ADR-0183 gains an amendment that states the rule.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- Named test: `Redact_TagThatIsItselfTheCommandWord_MasksTheInitialResponseOfBothReadings`. It and the mixed-word cases
  failed before the fix with the response in the output.
- Choice: withhold both readings (`**** ****`), as ADR-0183 amendment 3, point 2, does for `LOGIN LOGIN`. Recorded as
  amendment 5.
- Choice: a second word `AUTH` or `AUTHENTICATE` counts whatever the rule's own command words are. The SOCKS tunnel
  runs an `AUTH`-only rule beside one knowing both words and takes the first on a tie, so on `AUTH AUTHENTICATE ...`
  both must write the same line. This fixes the SOCKS case without touching the SOCKS library (BL-1707's).
- Verified: build `-warnaserror` clean, `dotnet format` clean, fast tests green (LineProtocol 200 of 200),
  `Measure-CodeQuality.ps1` reports 0 failing members for `Surl.LineProtocol.UnitLibrary`.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A SASL initial response on a line whose tag is AUTH or AUTHENTICATE is logged as ****.
