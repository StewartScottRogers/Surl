---
id: BL-1769
title: Withhold the whole quoted password of an inline AUTH <user> <password> line in SOCKS and HTTP proxy tunnels
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Documentation/Planning/Decisions/ADR-0183-a-sasl-line-redaction-rule-withholds-initial-responses-and-continuation-answers.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1769 — Withhold the whole quoted password of an inline AUTH <user> <password> line in SOCKS and HTTP proxy tunnels

## Goal

A quoted password containing spaces in an inline Redis `AUTH <user> "<password>"` line sent through a SOCKS or HTTP proxy tunnel is logged as `****` with none of its bytes after the mask.

## Context

Found while working BL-1768 by reading the code; not yet reproduced by a test. `SaslLineRedactionRule` splits words at spaces and tabs and masks only the third word, while Redis inline commands may quote an argument (`RedisInlineCommandSplitter`). So `AUTH alice "a b"` would be logged as `AUTH alice **** b"`. `--aihelp logging` and the manual say the password of that line is withheld. The same rule serves the SMTP, IMAP and POP3 servers, so their logging of lines with no quotes must not change. If masking cannot be fixed, say so in `AiHelpProse` and `ManualText` instead and add those projects to `touches`.

## Acceptance criteria

- [x] A test in the SOCKS tunnel rule's tests logs `AUTH alice "a b"` as `AUTH alice ****`.
- [x] A test in the HTTP proxy tunnel rule's tests logs the same line the same way.
- [x] The existing `SaslLineRedactionRuleTests` pass unchanged.
- [x] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

- Fix: `SaslLineRedactionRule` writes nothing after the first mask when the line holds a tab, `"` or `'`
  (ADR-0183 amendment 7, decided by Claude under Stewart's delegation). Lines with no quotes are logged as before.
- Tests: `SocksTunnelMailRedactionTests.Redis_InlineAuthWithAQuotedArgument_HasNothingLoggedAfterTheMask`,
  `HttpProxyRequestOrTunnelBytesRedactorTests.Redact_TunnelledInlineAuthWithAQuotedArgument_LogsNothingAfterTheMask`,
  and new rows in `TunnelLineRedactionRuleTests`. Ten failed before the fix; all pass after. `dotnet format
  --verify-no-changes` is clean.
- Added ADR-0183 to `touches` for the amendment; no task in Doing names it (BL-1762 holds ADR-0182).
- `AiHelpProse` and `ManualText` are unchanged: they already say that password is withheld.
- Pipeline: the change is a few lines, so plan, tests and implementation were done in this session; `code-reviewer`
  reviewed it (no must-fix). No conformance stage: only what the log writes changes, no byte on the wire.
- The review found older gaps (quoted command word; VT, FF or CR separators): filed as BL-1773.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A quoted inline AUTH password sent through a SOCKS or HTTP proxy tunnel is logged as **** with nothing after it
