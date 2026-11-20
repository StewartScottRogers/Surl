---
id: BL-1429
title: Withhold SMTP AUTH credentials from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419, BL-1428]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1429 — Withhold SMTP AUTH credentials from the exchange log

## Goal

`surl -v --user alice:s3cret --self-signed smtp://127.0.0.1:0/`, answering
`curl --ssl-reqd -k -u alice:s3cret --login-options AUTH=PLAIN --mail-from a@x --mail-rcpt alice@x -T m.eml`,
logs the `AUTH` line and any continuation answers in the redacted form BL-1417's ADR fixes.

## Context

- Bug report item: secrets in the `-v` log, which the parent named for SMTP `AUTH`.
- `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.cs:61` (`["AUTH"] = AnswerAuthAsync`). The
  server offers `334 ` continuations (ADR-0049). BL-1428's redactor does the work; this task wires it
  into the SMTP server and proves it, also after `STARTTLS`.
- `DATA` content is not a credential and stays as is.

## Acceptance criteria

- [x] The SMTP protocol server implements BL-1418's contract with BL-1428's redactor. Fast tests in
      `Protocols/Mail/Surl.Protocol.Smtp.UnitTests` show `AUTH PLAIN <b64>` and an `AUTH LOGIN`
      exchange logged in the ADR's form, also after `STARTTLS`.
- [x] A `DATA` body line `AUTH PLAIN x` is logged unchanged (named test).
- [x] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "TestCategory!=Integration"` stays
      green.
- [x] `dotnet build Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and coverage stays at 100% line and branch.

## Notes

- `SmtpLineRedactionRule` (internal) wraps `SaslLineRedactionRule("AUTH", "334 ")` and passes DATA lines (after a `354` reply until the `.` line) unchanged. Server implements `IExchangeBytesRedactingServer`. Tests: `SmtpExchangeLogRedactionTests`. Coverage reasoned by hand: every branch has a named test.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. SMTP server logs AUTH initial responses and 334 answers masked, DATA unchanged
