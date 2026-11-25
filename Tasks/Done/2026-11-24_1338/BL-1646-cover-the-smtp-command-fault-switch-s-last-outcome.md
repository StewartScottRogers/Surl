---
id: BL-1646
title: Cover the SMTP command-fault switch's last outcome
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1646 — Cover the SMTP command-fault switch's last outcome

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary` reports 100% line and 100% branch with no failing
member.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.Protocol.Smtp 100% line,
  99.74% branch, 1 failing member.
- `async SmtpSession.AnswerUnlessFaultedAsync`,
  `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.Faults.cs:23`, 91.67% branch. The `applied.Rule switch` at
  line 42 is 7 of 8. `Smtp.UnitTests` already has an Error-rule test, a Drop-rule test and an RCPT Delay-rule test that
  then answers, so first find which outcome is missing from the Cobertura `condition-coverage` of line 42. Candidates:
  a rule of another kind matching at command stage (`RefuseLogin`), or an `await` completing synchronously.
- IMAP (BL-1643) and POP3 (BL-1645) have the same gap in the same switch. Check their Logs first in case the answer is
  already known.

## Acceptance criteria

- [x] A named `Surl.Protocol.Smtp.UnitTests` test reaches line 42's missing outcome and asserts the reply bytes or
      connection outcome. If no test can reach it, the Log says so with the evidence, and the task moves to Blocked
      naming that, rather than adding an exclusion.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary` reports `100 | 100`
      with 0 failing members, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Tests only (test-writer). Record which outcome it was in the Log, for BL-1643 and BL-1645.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Missing outcome was the chosen-error reply write stalling (the await not completing synchronously), same as IMAP BL-1643's stalled fault-reply test; BL-1645 (POP3) likely the same. Added WriteStallingConnection and ServeAsync_ErrorReplyWriteStalls_ShutdownEndsTheSessionWithTheReplyUnwritten. SMTP 100 | 100, 0 failing.
- 2026-11-24: Doing -> Done. SMTP library at 100% line and branch, 0 failing members; stalled error-reply write test added
