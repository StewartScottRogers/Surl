---
id: BL-1756
title: Withhold a POP3 PASS password when spaces or tabs come before the command word
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary, Protocols/Mail/Surl.Protocol.Pop3.UnitTests, Documentation/Planning/Decisions/ADR-0183-a-sasl-line-redaction-rule-withholds-initial-responses-and-continuation-answers.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1756 — Withhold a POP3 PASS password when spaces or tabs come before the command word

## Goal

The POP3 exchange log writes the password as `****` on a `PASS` line that starts with spaces or tabs.

## Context

- Found while working BL-1751. `Pop3LineRedactionRule.IsPassWithArgument` matches `PASS` only at the start of the line,
  so `<space>PASS <password>` is logged with the password. The server refuses the line, but the client has already sent
  it. Read from the code; no test reproduces it yet.
- ADR-0183 amendment 3 holds how an IMAP `LOGIN` line splits words on runs of spaces and tabs.

## Acceptance criteria

- [x] A named test in `Surl.Protocol.Pop3.UnitTests` shows the password masked on `<space>PASS <password>`.
- [x] A named test shows it masked on `<tab>PASS<tab><password>`.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- Direct change: `Pop3LineRedactionRule` trims leading spaces and tabs before matching `PASS`; every byte before the mask is logged as received.
- Recorded as ADR-0183 amendment 6; added that ADR to `touches` (no task in Doing names it).
- The criterion tests are `Pass_AfterASpace_IsMaskedKeepingTheSpace` and `Pass_AfterATabAndSeparatedByATab_IsMaskedKeepingBothTabs`; both failed before the fix.
- Verified: build -warnaserror 0 warnings, format clean, fast tests 29476 passed, 0 failed, 3 skipped; POP3 library 100% line and branch coverage, 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The POP3 exchange log masks the PASS password when spaces or tabs come before the command word.
