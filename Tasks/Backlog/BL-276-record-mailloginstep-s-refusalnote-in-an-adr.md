---
id: BL-276
title: Record MailLoginStep's RefusalNote in an ADR
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-260]
touches: [Documentation/Planning/Decisions]
requirement: FR-046
created: 2026-09-30
completed:
---
# BL-276 — Record MailLoginStep's RefusalNote in an ADR

## Goal

An ADR marked "Decided by Claude under Stewart's delegation" records the contract change BL-260
made: `MailLoginStep`'s optional fifth member `RefusalNote`, which the SMTP, IMAP and POP3 servers
write to the exchange log after the `CheckedLogin` note.

## Context

- BL-260 added `string? RefusalNote = null` to `MailLoginStep`
  (`Surl.Protocol.Abstractions.UnitLibrary/MailLoginStep.cs`) so SASL `GSSAPI` can hand on
  ADR-0057 decision 4's `Kerberos: <reason>` line. `SaslMechanismExchange.RefuseAsync` takes it
  as an optional argument; `GssapiSaslExchange.AcceptTicketAsync` sets it from
  `KerberosAcceptResult.RefusalReason`.
- Rejected alternative: a reason field on `CheckedLogin`, whose `Note` is also used by HTTP,
  MQTT and the password logins, and whose one-line form `Login refused: <method> <user>` would
  have had to change.
- The note reaches only the verbose level because `LevelledExchangeLogFactory` lets no other note
  through below it (ADR-0033, section 3); `LevelledExchangeLogFactoryTests.KerberosRefusalNote_IsWrittenAtTheVerboseLevelOnly`
  pins it.
- Not written in BL-260 because `Documentation/Planning/Decisions` was named by BL-262 in `Doing`
  at the time (two lanes allocating the same ADR number).

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions` states the `RefusalNote` member, who
      sets it, where the servers write it, the rejected `CheckedLogin` alternative and why.
- [ ] ADR-0049 section 6 (or its amendments list) points at the new ADR.

## Notes

## Log

- 2026-09-30: Created.
