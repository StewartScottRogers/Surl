---
id: BL-1768
title: Say in --aihelp and the manual that inline Redis AUTH <password> and HELLO AUTH lines are logged through a SOCKS tunnel
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1766]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1768 — Say in --aihelp and the manual that inline Redis AUTH <password> and HELLO AUTH lines are logged through a SOCKS tunnel

## Goal

The `logging` topic of `surl --aihelp` and the manual say that a SOCKS tunnel logs the password of an inline Redis `AUTH <password>` or `HELLO ... AUTH` line.

## Context

Found while working BL-1766. `AiHelpProse.cs` and `ManualText.cs` list "Redis ones sent as RESP arrays" as the Redis logins not withheld in a SOCKS tunnel. The SOCKS rules (`SocksTunnelPasswordAndAuthorizationLineRule` and `SaslLineRedactionRule`) mask only the password of an inline `AUTH <user> <password>` line, so `AUTH <password>` and `HELLO 3 AUTH <user> <password>` are logged too (measured on the HTTP proxy tunnel, which holds the same rules). The proxy paragraph already says it.

## Acceptance criteria

- [x] The `logging` topic and the manual say the only Redis password withheld in a SOCKS tunnel is that of an inline `AUTH <user> <password>` line, pinned by `AiHelpTextTests` and `ManualTextTests`.
- [x] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

- The SOCKS "Not withheld" sentence now uses the HTTP proxy paragraph's Redis wording and names the two logged forms. It writes `HELLO <protover> AUTH`, because `SaslLineRedactionRule` does mask the password of a `HELLO AUTH <user> <password>` line with no protocol version (`AUTH` is its second word).
- Checked against `SocksTunnelLineRedactionRule` and `SaslLineRedactionRule`; no behaviour change, so no ADR.
- Verified: build `-warnaserror` 0 warnings, format check clean, fast tests 29554 passed, 3 skipped, 0 failed.
- Filed BL-1769: a quoted inline password containing spaces is only partly masked in a proxy tunnel (found by reading the code).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. --aihelp logging and the manual say a SOCKS tunnel logs inline Redis AUTH <password> and HELLO <protover> AUTH passwords
