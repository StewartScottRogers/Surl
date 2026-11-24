---
id: BL-1645
title: Cover the POP3 command-fault switch's last outcome
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-1645 — Cover the POP3 command-fault switch's last outcome

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Protocol.Pop3.UnitLibrary` reports 100% line and 100% branch with no failing
member.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.Protocol.Pop3 100% line,
  99.64% branch, 1 failing member.
- `async Pop3Session.AnswerUnlessFaultedAsync`,
  `Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary/Pop3Session.Faults.cs:30`, 91.67% branch. The `result.Rule switch` at
  line 54 is 7 of 8. `Pop3.UnitTests` already has an Error-rule test, a Drop-rule test and a RETR Delay-rule test that
  then answers, so first find which outcome is missing from the Cobertura `condition-coverage` of line 54. Candidates:
  a rule of another kind matching at command stage (`RefuseLogin`), or an `await` completing synchronously.
- IMAP (BL-1643) and SMTP (BL-1646) have the same gap in the same switch. Check their Logs first in case the answer is
  already known.

## Acceptance criteria

- [ ] A named `Surl.Protocol.Pop3.UnitTests` test reaches line 54's missing outcome and asserts the reply bytes or
      connection outcome. If no test can reach it, the Log says so with the evidence, and the task moves to Blocked
      naming that, rather than adding an exclusion.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Protocol.Pop3.UnitLibrary` reports `100 | 100`
      with 0 failing members, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Tests only (test-writer). Record which outcome it was in the Log, for BL-1643 and BL-1646.

## Log

- 2026-11-24: Created.
