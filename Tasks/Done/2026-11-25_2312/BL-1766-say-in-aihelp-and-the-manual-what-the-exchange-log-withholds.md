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
completed: 2026-11-25
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

- [x] The `logging` topic and the manual name the secrets withheld inside an HTTP proxy `CONNECT` tunnel.
- [x] Both name the accepted limits of ADR-0182 amendment 16 that leave a secret logged.
- [x] `ManualTextTests` pins the manual, and build with `-warnaserror` is clean and the fast tests are green.

## Notes

- `logging` and the manual now hold three paragraphs on the proxy `CONNECT` tunnel: what is masked, what is not, and
  the accepted cases that log a secret. The `security` sentence says the proxy tunnel masks the text-protocol secrets
  and HTTP credentials, not an MQTT password or an LDAP bind.
- Beyond amendment 16's list, the help names what the code logs: Redis logins other than inline `AUTH <user> <password>`,
  `PASS`/`ACCT` after a leading blank or before a tab, and an IMAP `LOGIN` with no tag or with a blank before its tag.
  These and the limit cases were checked with a temporary test over `HttpProxyRequestOrTunnelBytesRedactor`, then removed.
- Amendment 9's two-space initial response limit is closed (BL-1738), so it is not named.
- Verified: build `-warnaserror` clean, format clean, fast tests 29554 passed, 0 failed, 3 skipped.
- Follow-up filed: BL-1768 (the SOCKS paragraph understates which Redis logins it logs).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. --aihelp logging and security and the manual say what the exchange log withholds inside an HTTP proxy CONNECT tunnel and which accepted cases log a secret
