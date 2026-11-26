---
id: BL-1763
title: Remove ADR-0182's limits that BL-1738 and BL-1735 closed
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1763 — Remove ADR-0182's limits that BL-1738 and BL-1735 closed

## Goal

ADR-0182 no longer presents the work of BL-1738 or BL-1735 as still open.

## Context

- File: `Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md`.
- Amendment 9's "Accepted limits" says "An initial response after two spaces is logged, as in the mail servers' own
  logs (BL-1738)". BL-1738 made that false: `Protocols/Mail/Surl.LineProtocol.UnitLibrary/SaslLineRedactionRule.cs`
  separates words by runs of spaces and tabs.
- Amendment 8's "Accepted limits" says "Saying so in the help text is BL-1735", which reads as pending. BL-1735 is
  Done (`AiHelpProse.cs` and `ManualText.cs` in `Application/Surl.Cli.UnitLibrary` say it).
- Found while working BL-1730. BL-1731 and BL-1739 also edit this file.
- Documentation only: no store, event or fault for the stand-in changes.

## Acceptance criteria

- [ ] The two-spaces bullet under Amendment 9's accepted limits is removed, or states that BL-1738 closed the gap.
- [ ] Amendment 8 states that the help text says a misplaced preface withholds the rest, not that saying so is pending.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing.
