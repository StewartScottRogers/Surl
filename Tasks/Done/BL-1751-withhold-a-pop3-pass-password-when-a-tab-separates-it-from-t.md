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
completed: 2026-11-25
---
# BL-1751 — Withhold a POP3 PASS password when a tab separates it from the command word

## Goal

The POP3 exchange log writes the password as `****` on a `PASS<tab><password>` line.

## Context

- Found while working BL-1749. `Pop3LineRedactionRule.IsPassWithArgument` requires a space after `PASS`, so a
  tab-separated line is logged with the password. Read from the code; no test reproduces it yet.
- ADR-0183 amendments 3 and 4 hold how IMAP `LOGIN` and SASL lines treat tabs.

## Acceptance criteria

- [x] A named test in `Surl.Protocol.Pop3.UnitTests` shows the password masked on `PASS<tab><password>`.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- `Pop3ExchangeLogRedactionTests.Pass_SeparatedByATab_IsMaskedKeepingTheTab` failed before the fix (the password was
  logged) and passes after it.
- Choice: the separator is logged as received (`PASS<tab>****`), as ADR-0183 amendment 4 does for separators before the
  mask. Nothing is written after the mask, so a password holding further tabs is withheld whole. No new ADR: the Goal
  states the behaviour and no byte Surl sends changes.
- No upstream curl measurement: curl sends a single space, and only what the log writes changes.
- Verified 2026-11-25: `dotnet build -warnaserror` 0 warnings; fast tests no failures (243 in Pop3);
  `Measure-CodeQuality.ps1 -Library Surl.Protocol.Pop3.UnitLibrary` 100% line, 100% branch, 0 failing members;
  `dotnet format --verify-no-changes` on both projects clean.
- Set aside, filed as BL-1756: a `PASS` line that starts with spaces or tabs is still logged with the password.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The POP3 exchange log masks the password on a tab-separated PASS line
