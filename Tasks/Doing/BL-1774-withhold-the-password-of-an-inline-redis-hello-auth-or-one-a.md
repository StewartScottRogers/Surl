---
id: BL-1774
title: Withhold the password of an inline Redis HELLO AUTH or one-argument AUTH line in proxy tunnels
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Documentation/Planning/Decisions/ADR-0183-a-sasl-line-redaction-rule-withholds-initial-responses-and-continuation-answers.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1774 — Withhold the password of an inline Redis HELLO AUTH or one-argument AUTH line in proxy tunnels

## Goal

No password byte of an inline Redis `HELLO <version> AUTH <user> <password>` or `AUTH <password>` line in a SOCKS or HTTP
proxy tunnel is logged.

## Context

- Found while delivering BL-1773, by reading `SaslLineRedactionRule` and `TunnelRedisAuthLineRule`; not reproduced by a
  test. The first looks for `AUTH` as the first or second word and masks a third word, so neither line is masked.
- ADR-0182's table lists `HELLO ... AUTH` for the Redis server. ADR-0183, amendment 8, lists the one-argument form as
  not covered.
- SMTP, IMAP and POP3 logging must not change, and `AUTH PLAIN` with no initial response holds no secret.

## Acceptance criteria

- [ ] `TunnelLineRedactionRuleTests` log `HELLO 3 AUTH alice s3cret` and, after another line, `AUTH s3cret`, with no byte of `s3cret`.
- [ ] The existing `SaslLineRedactionRuleTests` pass unchanged.
- [ ] ADR-0183 states what is covered, and `dotnet build -warnaserror` and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
