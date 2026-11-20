---
id: BL-1430
title: Withhold IMAP LOGIN and AUTHENTICATE credentials from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419, BL-1428]
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1430 — Withhold IMAP LOGIN and AUTHENTICATE credentials from the exchange log

## Goal

`surl -v --user alice:s3cret --self-signed imaps://127.0.0.1:0/`, answering
`curl -k -u alice:s3cret "imaps://127.0.0.1:<port>/INBOX?ALL"`, logs the `LOGIN` password and every
`AUTHENTICATE` response in the redacted form BL-1417's ADR fixes.

## Context

- Bug report item: secrets in the `-v` log, which the parent named for IMAP `AUTH` and `LOGIN`.
- `Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/ImapSession.cs:69` (`LOGIN`) and `:71`
  (`AUTHENTICATE`). `LOGIN`'s password may be an atom, a quoted string, or a literal (`{6}` then the
  bytes on the next line, after a `+` continuation; RFC 3501 section 4.3), so the redactor follows the
  literal's byte count. `AUTHENTICATE` uses BL-1428's continuation redactor with the `+ ` prefix and
  SASL-IR.
- Command tags come first (`A001 LOGIN alice s3cret`).

## Acceptance criteria

- [ ] The IMAP protocol server implements BL-1418's contract. Fast tests in
      `Protocols/Mail/Surl.Protocol.Imap.UnitTests` show `A1 LOGIN alice s3cret`,
      `A1 LOGIN alice "s3 cret"` and `A1 LOGIN alice {6}` with the literal on the next line all logged
      in the ADR's form.
- [ ] `A2 AUTHENTICATE PLAIN <b64>` (SASL-IR) and a continuation-based `AUTHENTICATE` are redacted
      (named tests).
- [ ] An `APPEND` literal whose message contains `LOGIN a b` is logged unchanged (named test).
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "TestCategory!=Integration"` stays
      green.
- [ ] `dotnet build Protocols/Mail/Surl.Protocol.Imap.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
