---
id: BL-1764
title: Update --aihelp and the manual once the open exchange-log redaction gaps are closed
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1734, BL-1744, BL-1750, BL-1753, BL-1758, BL-1759, BL-1760, BL-1762, BL-1771]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1764 — Update --aihelp and the manual once the open exchange-log redaction gaps are closed

## Goal

The `logging` and `security` topics of `surl --aihelp` and the manual no longer name as logged any secret that the
tasks in `depends-on` made the exchange log withhold.

## Context

- BL-1723 wrote the lists as the code was on 2026-11-25, naming each open gap as logged as received: FTP and mail
  logins in an HTTP proxy tunnel (BL-1734), MQTT 5 Authentication Data in a SOCKS tunnel (BL-1744), a SOCKS-tunnelled
  IMAP `LOGIN` with no tag or with tabs (BL-1750) and `PASS` or `ACCT` with tabs (BL-1753), bytes in place of the HTTP/2
  preface (BL-1758), API-key headers through the HTTP proxy (BL-1759) and the SOCKS tunnel (BL-1760), and a split
  HTTP/3 `HEADERS` frame (BL-1762).
- Text: `AiHelpProse.cs` (`logging`, `security`) and `ManualText.cs` (`Application/Surl.Cli.UnitLibrary`), pinned by
  `AiHelpTextTests` and `ManualTextTests`.
- Write what the code does when this task runs (ADR-0182 and its amendments). BL-1753 and BL-1758 may close only part
  of a sentence (a leading blank, another case), so read the redactors before removing it.

## Acceptance criteria

- [ ] The `logging` topic and the manual name no secret as logged that the code withholds.
- [ ] The `security` topic's proxy tunnel sentence matches the code.
- [ ] `AiHelpTextTests` and `ManualTextTests` pin the changed sentences.
- [ ] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-26: BL-1771 added to `depends-on`: it closed the bare-line-feed request head case both texts still name as
  logged (ADR-0182 amendment 24, whose accepted limits name what can still be logged).
