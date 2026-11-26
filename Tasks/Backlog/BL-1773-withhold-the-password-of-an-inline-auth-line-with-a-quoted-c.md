---
id: BL-1773
title: Withhold the password of an inline AUTH line with a quoted command word or a VT, FF or CR separator in proxy tunnels
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Documentation/Planning/Decisions/ADR-0183-a-sasl-line-redaction-rule-withholds-initial-responses-and-continuation-answers.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1773 — Withhold the password of an inline AUTH line with a quoted command word or a VT, FF or CR separator in proxy tunnels

## Goal

No password byte of an inline Redis `AUTH <user> <password>` line in a SOCKS or HTTP proxy tunnel is logged when the command word is quoted or a vertical tab, form feed or bare carriage return separates its arguments.

## Context

- Found by code review of BL-1769, by reading the code; not reproduced by a test.
- `SaslLineRedactionRule` splits words at spaces and tabs only and matches the command word unquoted, while
  `RedisInlineCommandSplitter` accepts `"AUTH"` and treats VT, FF and CR as white space (ADR-0183, amendment 7, point 3).
- The rule also serves SMTP, IMAP and POP3, whose logging of lines with no quotes must not change; a tunnel-only rule is
  one way. A tunnel whose first byte is a quote never reaches the line rules (ADR-0182, amendments 9 and 16).

## Acceptance criteria

- [ ] `TunnelLineRedactionRuleTests` log `AUTH alice s3cret` with its spaces replaced by VT, FF or CR, and `AUTH "alice" s3cret` and `"AUTH" alice s3cret` after another line, with no byte of `s3cret`.
- [ ] The existing `SaslLineRedactionRuleTests` pass unchanged.
- [ ] ADR-0183 states what is covered, and `dotnet build -warnaserror` and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
