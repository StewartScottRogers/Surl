---
id: BL-1752
title: Withhold an FTP PASS or ACCT argument when a tab separates it from the command word
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1752 — Withhold an FTP PASS or ACCT argument when a tab separates it from the command word

## Goal

The FTP exchange log writes the argument as `****` on a `PASS<tab><password>` or `ACCT<tab><account>` line.

## Context

- Found while working BL-1749. `FtpLineRedactionRule.CarriesSecret` requires a space after the command word, so a
  tab-separated line is logged with the secret. Read from the code; no test reproduces it yet.
- ADR-0182 decisions 1 and 6 set the rule.

## Acceptance criteria

- [x] A named test in `Surl.Protocol.Ftp.UnitTests` shows the argument masked on `PASS<tab><password>`.
- [x] A named test shows it masked on `ACCT<tab><account>`.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- Tests: `Pass_SeparatedByATab_IsMaskedKeepingTheTab` and `Acct_SeparatedByATab_IsMaskedKeepingTheTab` in
  `FtpExchangeLogRedactionTests`; both failed before the fix.
- `FtpLineRedactionRule` accepts a space or a tab after the command word and logs the separator as sent. What the
  server answers is unchanged: it still splits a command at the first space only.
- No ADR amendment: ADR-0182 decision 1 already requires the secret to be masked, and the rule's doc comment records
  the tab.
- Verified 2026-11-25: build `-warnaserror` 0 warnings, format check clean, fast tests 29434 passed, 0 failed,
  3 skipped; `Measure-CodeQuality.ps1` reports 0 failing members for the library.
- Filed BL-1755: a line after a bare line feed is still logged with its password.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The FTP exchange log masks the PASS or ACCT argument when a tab separates it from the command word
