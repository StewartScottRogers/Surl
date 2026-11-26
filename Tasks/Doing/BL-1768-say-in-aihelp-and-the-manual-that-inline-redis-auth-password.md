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
completed:
---
# BL-1768 — Say in --aihelp and the manual that inline Redis AUTH <password> and HELLO AUTH lines are logged through a SOCKS tunnel

## Goal

The `logging` topic of `surl --aihelp` and the manual say that a SOCKS tunnel logs the password of an inline Redis `AUTH <password>` or `HELLO ... AUTH` line.

## Context

Found while working BL-1766. `AiHelpProse.cs` and `ManualText.cs` list "Redis ones sent as RESP arrays" as the Redis logins not withheld in a SOCKS tunnel. The SOCKS rules (`SocksTunnelPasswordAndAuthorizationLineRule` and `SaslLineRedactionRule`) mask only the password of an inline `AUTH <user> <password>` line, so `AUTH <password>` and `HELLO 3 AUTH <user> <password>` are logged too (measured on the HTTP proxy tunnel, which holds the same rules). The proxy paragraph already says it.

## Acceptance criteria

- [ ] The `logging` topic and the manual say the only Redis password withheld in a SOCKS tunnel is that of an inline `AUTH <user> <password>` line, pinned by `AiHelpTextTests` and `ManualTextTests`.
- [ ] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
