---
id: BL-259
title: Log SASL GSSAPI's Kerberos refusal reason at the verbose level
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-218]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests, Surl.Protocol.Imap.UnitLibrary, Surl.Protocol.Imap.UnitTests, Surl.Protocol.Pop3.UnitLibrary, Surl.Protocol.Pop3.UnitTests]
requirement: FR-046
created: 2026-09-30
completed:
---
# BL-259 — Log SASL GSSAPI's Kerberos refusal reason at the verbose level

## Goal

When SASL `GSSAPI` refuses a Kerberos ticket, the mail server's verbose log carries ADR-0057
decision 4's line `Kerberos: <reason>` (such as `Kerberos: ticket expired`), and no key byte or
decrypted field.

## Context

- ADR-0057 decision 4: every refusal is `RefusedCredentials`, and "the reason goes to the verbose
  log only, in the words `Kerberos: <reason>`".
- BL-218 built `Surl.Authentication.UnitLibrary/GssapiSaslExchange.cs`, which receives the reason
  in `KerberosAcceptResult.RefusalReason` but has no way to hand it on: `MailLoginStep`
  (`Surl.Protocol.Abstractions`) carries no log text, and the SMTP, IMAP and POP3 servers log only
  what the step carries.
- Decide the smallest contract change (for example an optional reason on `MailLoginStep`, or a
  field on `CheckedLogin`) and record it in an ADR if it changes the contract.

## Acceptance criteria

- [ ] A fast test in `Surl.Authentication.UnitTests` shows an expired ticket's refusal step carries
      the reason `ticket expired`, and an accepted login none.
- [ ] A fast test in each of `Surl.Protocol.Smtp.UnitTests`, `Surl.Protocol.Imap.UnitTests` and
      `Surl.Protocol.Pop3.UnitTests` shows the verbose log line `Kerberos: <reason>` for a refused
      `GSSAPI` step, and no such line below the verbose level.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-218 (2026-09-30).

## Log

- 2026-09-30: Created.
