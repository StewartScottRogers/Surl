---
id: BL-1756
title: Withhold a POP3 PASS password when spaces or tabs come before the command word
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary, Protocols/Mail/Surl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-11-25
completed:
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

- [ ] A named test in `Surl.Protocol.Pop3.UnitTests` shows the password masked on `<space>PASS <password>`.
- [ ] A named test shows it masked on `<tab>PASS<tab><password>`.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
