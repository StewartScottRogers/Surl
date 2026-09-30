---
id: BL-260
title: Log SASL GSSAPI's Kerberos refusal reason at the verbose level
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-218]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests, Surl.Protocol.Imap.UnitLibrary, Surl.Protocol.Imap.UnitTests, Surl.Protocol.Pop3.UnitLibrary, Surl.Protocol.Pop3.UnitTests, Surl.Output.UnitTests]
requirement: FR-046
created: 2026-09-30
completed: 2026-09-30
---
# BL-260 — Log SASL GSSAPI's Kerberos refusal reason at the verbose level

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

- [x] A fast test in `Surl.Authentication.UnitTests` shows an expired ticket's refusal step carries
      the reason `ticket expired`, and an accepted login none.
- [x] A fast test in each of `Surl.Protocol.Smtp.UnitTests`, `Surl.Protocol.Imap.UnitTests` and
      `Surl.Protocol.Pop3.UnitTests` shows the verbose log line `Kerberos: <reason>` for a refused
      `GSSAPI` step, and no such line below the verbose level.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-218 (2026-09-30).
- Decision: an optional fifth member `string? RefusalNote = null` on `MailLoginStep`, holding the
  whole log line (`Kerberos: ticket expired`), so the servers write it verbatim after the
  `CheckedLogin` note without knowing any mechanism. Rejected: a field on `CheckedLogin`, whose
  `Note` HTTP, MQTT and the password logins share. Existing four-argument constructions compile
  unchanged. `SaslMechanismExchange.RefuseAsync` takes it as an optional argument;
  `GssapiSaslExchange` sets it from `KerberosAcceptResult.RefusalReason` for a refused ticket only.
- Verbose only: servers write every note unconditionally and `LevelledExchangeLogFactory` lets
  none but the engine's own through below verbose (ADR-0033, section 3). The protocol test projects
  do not reference `Surl.Output`, so the "no such line below verbose" half is pinned in
  `Surl.Output.UnitTests` (`LevelledExchangeLogFactoryTests.KerberosRefusalNote_IsWrittenAtTheVerboseLevelOnly`);
  added to `touches` because no task in Doing names it.
- ADR not written here: `Documentation/Planning/Decisions` is in BL-262's `touches` (Doing), and two
  lanes writing ADRs at once would collide on the number. Filed BL-275 to record it.
- Tests: `GssapiSaslMechanismTests` (expired ticket carries `Kerberos: ticket expired`, wrong key
  `Kerberos: integrity check failed`, accepted login none);
  `ServeAsync_RefusedGssapiTicket_NotesTheKerberosReasonAfterTheLogin` in SMTP, IMAP and POP3.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. A refused SASL GSSAPI ticket now writes 'Kerberos: <reason>' to the SMTP, IMAP and POP3 verbose log
