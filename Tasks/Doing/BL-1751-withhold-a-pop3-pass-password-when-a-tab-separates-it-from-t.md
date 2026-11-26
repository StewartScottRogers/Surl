---
id: BL-1751
title: Withhold a POP3 PASS password when a tab separates it from the command word
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary, Protocols/Mail/Surl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1751 — Withhold a POP3 PASS password when a tab separates it from the command word

## Goal

The POP3 exchange log writes the password as `****` on a `PASS<tab><password>` line.

## Context

- Found while working BL-1749. `Pop3LineRedactionRule.IsPassWithArgument` requires a space after `PASS`, so a
  tab-separated line is logged with the password. Read from the code; no test reproduces it yet.
- ADR-0183 amendments 3 and 4 hold how IMAP `LOGIN` and SASL lines treat tabs.

## Acceptance criteria

- [ ] A named test in `Surl.Protocol.Pop3.UnitTests` shows the password masked on `PASS<tab><password>`.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
