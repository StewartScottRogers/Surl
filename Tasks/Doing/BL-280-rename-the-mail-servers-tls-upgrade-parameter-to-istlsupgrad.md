---
id: BL-280
title: Rename the mail servers' TLS-upgrade parameter to isTlsUpgradeAvailable
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-274]
touches: [Surl.Protocol.Imap.UnitLibrary, Surl.Protocol.Imap.UnitTests, Surl.Protocol.Pop3.UnitLibrary, Surl.Protocol.Pop3.UnitTests, Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-09-30
completed:
---
# BL-280 — Rename the mail servers' TLS-upgrade parameter to isTlsUpgradeAvailable

## Goal

The SMTP, IMAP and POP3 servers name the setting "whether this server offers a TLS upgrade"
`isTlsUpgradeAvailable`, as FTP and `Surl.Console` already do, so one concept has one name
(ADR-0069).

## Context

- ADR-0069 (BL-274) decides one name, `isTlsUpgradeAvailable`, for the constructor parameter, the
  field behind it and the test helpers' parameters, rejecting a per-command name.
- Today: `isStartTlsAvailable` in `SmtpProtocolServer`, `SmtpSession`, `ImapProtocolServer`,
  `ImapSession` (and `ImapSession.Authentication.cs`); `isStlsAvailable` in `Pop3ProtocolServer`
  and `Pop3Session`. Tests: `SmtpTestExchange`, `SmtpAuthTests`, `ImapTestExchange`,
  `ImapStartTlsTests`, `RecordedFixtureTests` (IMAP, POP3), `Pop3TestExchange`, `Pop3StlsTests`,
  `Pop3AuthTests`, `Pop3ApopTests`. The libraries' own `CLAUDE.md` files name them too.
- `Surl.Console` passes these arguments positionally, so it needs no change.
- Renames only; nothing on the wire changes. Test method names that name the protocol command
  (`STARTTLS`, `STLS`) stay: they describe the exchange, not the parameter.

## Acceptance criteria

- [ ] `rg -n "isStartTlsAvailable|isStlsAvailable" --glob "*.cs"` over the solution finds nothing.
- [ ] `rg -n "isStartTlsAvailable|isStlsAvailable"` finds nothing in
      `Surl.Protocol.Smtp.UnitLibrary/CLAUDE.md`, `Surl.Protocol.Imap.UnitLibrary/CLAUDE.md` or
      `Surl.Protocol.Pop3.UnitLibrary/CLAUDE.md`.
- [ ] Each changed `<param>` doc comment says what the flag means under the new name.
- [ ] `Documentation/Wiki/Glossary.md`'s "TLS upgrade" row names `isTlsUpgradeAvailable` as the
      one name, from `CommandLineRunner.ComposeProtocolServers` into every server's constructor,
      and cites ADR-0069.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Filed by BL-274 (ADR-0069).
- 2026-09-30: Backlog -> Doing.
