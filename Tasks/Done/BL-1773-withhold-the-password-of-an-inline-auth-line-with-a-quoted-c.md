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
completed: 2026-11-25
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

- [x] `TunnelLineRedactionRuleTests` log `AUTH alice s3cret` with its spaces replaced by VT, FF or CR, and `AUTH "alice" s3cret` and `"AUTH" alice s3cret` after another line, with no byte of `s3cret`.
- [x] The existing `SaslLineRedactionRuleTests` pass unchanged.
- [x] ADR-0183 states what is covered, and `dotnet build -warnaserror` and the fast tests are green.

## Notes

- Choice: a tunnel-only rule, `TunnelRedisAuthLineRule`, so `SaslLineRedactionRule` and the mail servers' logs are
  unchanged (ADR-0183, amendment 8).
- It masks everything after the command word, losing the user name: a mask from there starts before any SASL
  initial-response mask. A mask at the password would not: `AUTH alice <VT> s3cret` would be logged by the SASL rule as
  `AUTH alice **** s3cret`.
- Verified: build with `-warnaserror` clean, fast tests green (`Surl.LineProtocol.UnitTests` 326 passed), format check
  clean, 100% line and branch coverage on the new and changed classes.
- Code review found no leak within the goal; its two ADR wording points and one simplification are applied.
- Follow-up: BL-1774 (`HELLO ... AUTH` and one-argument `AUTH`).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A tunnelled Redis AUTH line with a quoted command word or a VT, FF or CR is logged with no password byte
