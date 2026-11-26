---
id: BL-1769
title: Withhold the whole quoted password of an inline AUTH <user> <password> line in SOCKS and HTTP proxy tunnels
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1769 — Withhold the whole quoted password of an inline AUTH <user> <password> line in SOCKS and HTTP proxy tunnels

## Goal

A quoted password containing spaces in an inline Redis `AUTH <user> "<password>"` line sent through a SOCKS or HTTP proxy tunnel is logged as `****` with none of its bytes after the mask.

## Context

Found while working BL-1768 by reading the code; not yet reproduced by a test. `SaslLineRedactionRule` splits words at spaces and tabs and masks only the third word, while Redis inline commands may quote an argument (`RedisInlineCommandSplitter`). So `AUTH alice "a b"` would be logged as `AUTH alice **** b"`. `--aihelp logging` and the manual say the password of that line is withheld. The same rule serves the SMTP, IMAP and POP3 servers, so their logging of lines with no quotes must not change. If masking cannot be fixed, say so in `AiHelpProse` and `ManualText` instead and add those projects to `touches`.

## Acceptance criteria

- [ ] A test in the SOCKS tunnel rule's tests logs `AUTH alice "a b"` as `AUTH alice ****`.
- [ ] A test in the HTTP proxy tunnel rule's tests logs the same line the same way.
- [ ] The existing `SaslLineRedactionRuleTests` pass unchanged.
- [ ] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
