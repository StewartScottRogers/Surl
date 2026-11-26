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
completed:
---
# BL-1752 — Withhold an FTP PASS or ACCT argument when a tab separates it from the command word

## Goal

The FTP exchange log writes the argument as `****` on a `PASS<tab><password>` or `ACCT<tab><account>` line.

## Context

- Found while working BL-1749. `FtpLineRedactionRule.CarriesSecret` requires a space after the command word, so a
  tab-separated line is logged with the secret. Read from the code; no test reproduces it yet.
- ADR-0182 decisions 1 and 6 set the rule.

## Acceptance criteria

- [ ] A named test in `Surl.Protocol.Ftp.UnitTests` shows the argument masked on `PASS<tab><password>`.
- [ ] A named test shows it masked on `ACCT<tab><account>`.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
