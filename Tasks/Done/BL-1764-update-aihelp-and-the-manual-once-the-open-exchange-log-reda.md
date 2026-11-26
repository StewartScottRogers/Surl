---
id: BL-1764
title: Update --aihelp and the manual once the open exchange-log redaction gaps are closed
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1734, BL-1744, BL-1750, BL-1753, BL-1758, BL-1759, BL-1760, BL-1762, BL-1771, BL-1780]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-26
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
- BL-1765 and BL-1780 (ADR-0182 amendments 23 and 25): both proxies now withhold an MQTT `CONNECT` password and LDAP
  bind credentials, at a tunnel's first byte and after a first line starting `PROXY `. The texts still say the HTTP
  proxy does not, and that the SOCKS tunnel's first byte alone chooses.
- Text: `AiHelpProse.cs` (`logging`, `security`) and `ManualText.cs` (`Application/Surl.Cli.UnitLibrary`), pinned by
  `AiHelpTextTests` and `ManualTextTests`.
- Write what the code does when this task runs (ADR-0182 and its amendments). BL-1753 and BL-1758 may close only part
  of a sentence (a leading blank, another case), so read the redactors before removing it.

## Acceptance criteria

- [x] The `logging` topic and the manual name no secret as logged that the code withholds.
- [x] The `security` topic's proxy tunnel sentence matches the code.
- [x] The `logging` topic and the manual say that both proxies choose the MQTT and LDAP redactors at a tunnel's first
      byte or after a first `PROXY` line.
- [x] `AiHelpTextTests` and `ManualTextTests` pin the changed sentences.
- [x] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

- Rewrote the `logging` and `security` paragraphs in `AiHelpProse.cs` and the matching `ManualText.cs` lines from the
  redactors as they are, with ADR-0182 amendments 13 to 30 and ADR-0183 amendments 8 and 9.
- Removed as closed: API-key headers through either proxy, tunnelled MQTT passwords, Authentication Data and LDAP binds,
  tab-separated `PASS`/`ACCT`, `LOGIN` with no tag or with tabs, bytes in place of the HTTP/2 preface, split HTTP/3
  `HEADERS` frames, and bare-line-feed request heads.
- Choice: where a removed sentence left a narrower gap, the texts now name it, so they do not claim more than the code
  does: the accepted limits of amendments 17, 19, 23 and 25 and the open trailer (BL-1770) and leading-blank `PASS`
  (BL-1783) gaps. BL-1784 rewrites them once BL-1770, BL-1772 and BL-1783 are done.
- No ADR: no design or behaviour decision, text only.
- Verify: build `-warnaserror` clean, format clean, fast tests 29978 passed, 3 skipped, 0 failed. No integration run:
  no network, file system or database code changed.

## Log

- 2026-11-25: Created.
- 2026-11-26: BL-1771 added to `depends-on`: it closed the bare-line-feed request head case both texts still name as
  logged (ADR-0182 amendment 24, whose accepted limits name what can still be logged).
- 2026-11-26: BL-1780 added to `depends-on`, with a context bullet and a criterion for the proxy tunnel sentences
  that BL-1765 and BL-1780 made out of date.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. --aihelp and the manual now say what both proxies' exchange logs withhold, and name only gaps still open
