---
id: BL-1766
title: Say in --aihelp and the manual what the exchange log withholds inside an HTTP proxy CONNECT tunnel
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1734, BL-1723]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1766 — Say in --aihelp and the manual what the exchange log withholds inside an HTTP proxy CONNECT tunnel

## Goal

The `logging` and `security` topics of `surl --aihelp` and the manual say which logins the exchange log withholds inside
an HTTP proxy `CONNECT` tunnel, and which it does not.

## Context

- BL-1734 (ADR-0182 amendment 16) made the proxy mask FTP `PASS` and `ACCT` and SMTP, IMAP and POP3 credentials in a
  `CONNECT` tunnel. `AiHelpProse.cs` and `ManualText.cs` describe the SOCKS tunnel's secrets but not these.
- BL-1723 edits the same lists, so this task follows it. Write what the code does when this task runs.

## Acceptance criteria

- [ ] The `logging` topic and the manual name the secrets withheld inside an HTTP proxy `CONNECT` tunnel.
- [ ] Both name the accepted limits of ADR-0182 amendment 16 that leave a secret logged.
- [ ] `ManualTextTests` pins the manual, and build with `-warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
