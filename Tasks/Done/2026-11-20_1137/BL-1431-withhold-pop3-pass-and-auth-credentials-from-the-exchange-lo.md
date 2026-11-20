---
id: BL-1431
title: Withhold POP3 PASS and AUTH credentials from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419, BL-1428]
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary, Protocols/Mail/Surl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1431 — Withhold POP3 PASS and AUTH credentials from the exchange log

## Goal

`surl -v --user alice:s3cret --self-signed pop3s://127.0.0.1:0/`, answering
`curl -k -u alice:s3cret pop3s://127.0.0.1:<port>/`, logs `PASS` and every `AUTH` response in the
redacted form BL-1417's ADR fixes.

## Context

- Bug report item: secrets in the `-v` log, which the parent named for POP3 `AUTH` and `LOGIN`.
- `Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary/Pop3Session.cs:57` (`PASS`), `:59` (`APOP`, whose
  MD5 digest is not the password; the ADR says whether to redact it) and `:60` (`AUTH`). `PASS` uses
  BL-1418's line redactor; `AUTH` uses BL-1428's continuation redactor with the `+ ` prefix.

## Acceptance criteria

- [x] The POP3 protocol server implements BL-1418's contract. Fast tests in
      `Protocols/Mail/Surl.Protocol.Pop3.UnitTests` show `PASS s3cret` (also lower case and split across
      reads) logged in the ADR's form.
- [x] `AUTH PLAIN <b64>` and a continuation-based `AUTH LOGIN` are redacted; `APOP` is logged as the
      ADR says (named tests).
- [x] A `RETR` reply or a client line `USER alice` is logged unchanged.
- [x] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "TestCategory!=Integration"` stays
      green.
- [x] `dotnet build Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and coverage stays at 100% line and branch.

## Notes

- Pop3LineRedactionRule masks PASS and delegates AUTH to the shared SaslLineRedactionRule ("AUTH", "+ "). APOP is logged unchanged per ADR-0182 decision 8 (a digest, not the password), so no new ADR. Pop3ProtocolServer implements IExchangeBytesRedactingServer. 238 Pop3 tests and 1053 conformance fast tests green.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. POP3 PASS and AUTH credentials are withheld from the exchange log; APOP logged per ADR-0182
