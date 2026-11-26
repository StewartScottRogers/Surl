---
id: BL-1774
title: Withhold the password of an inline Redis HELLO AUTH or one-argument AUTH line in proxy tunnels
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Documentation/Planning/Decisions/ADR-0183-a-sasl-line-redaction-rule-withholds-initial-responses-and-continuation-answers.md, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
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

- [x] `TunnelLineRedactionRuleTests` log `HELLO 3 AUTH alice s3cret` and, after another line, `AUTH s3cret`, with no byte of `s3cret`.
- [x] The existing `SaslLineRedactionRuleTests` pass unchanged.
- [x] ADR-0183 states what is covered, and `dotnet build -warnaserror` and the fast tests are green.

## Notes

- Design (ADR-0183, amendment 9, decided by Claude under Stewart's delegation): both lines are handled by the
  tunnel-only `TunnelRedisAuthLineRule`, so the SMTP, IMAP and POP3 logs do not change. A `HELLO` line is masked from
  after its `AUTH` word, which loses the user name and needs no Redis splitter.
- A one-argument `AUTH` is masked unless the argument is, unquoted, a SASL or FTP security mechanism name. Why: `AUTH
  PLAIN`, `AUTH LOGIN` and FTP's `AUTH TLS` hold no secret. A password equal to such a name is logged; the ADR, the
  manual and `--aihelp` say so.
- A quoted password given alone (`AUTH "a password"`, amendment 7, point 3) is also an `AUTH <password>` line, so it is
  covered.
- Added `Application/Surl.Cli.UnitLibrary` and `Application/Surl.Cli.UnitTests` to `touches`: their `--aihelp` and
  manual text said these two lines are logged, which this change made false (root `CLAUDE.md`, "Say what it does").
  The only other task in `Doing`, BL-1762, names neither.
- 29 new test cases failed before the fix. After it: `dotnet build -warnaserror` clean, fast tests green,
  `Surl.LineProtocol.UnitTests` 388 passed, `TunnelRedisAuthLineRule` at 100% line and branch coverage.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. Inline Redis AUTH <password> and HELLO ... AUTH lines in SOCKS and HTTP proxy tunnels are logged with the password as **** (ADR-0183, amendment 9)
