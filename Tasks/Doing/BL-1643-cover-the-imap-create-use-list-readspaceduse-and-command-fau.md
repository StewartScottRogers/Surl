---
id: BL-1643
title: Cover the IMAP CREATE USE-list, ReadSpacedUse and command-fault branches
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-1643 — Cover the IMAP CREATE USE-list, ReadSpacedUse and command-fault branches

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary` reports 100% line and 100% branch with no failing
member.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.Protocol.Imap 100% line,
  99.81% branch, 3 failing members.
- `ImapSession.CreateMailbox(ImapArguments)`, `Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/ImapSession.Changes.cs:113`,
  93.75% branch, Cx 9. This is the most complex member with a gap. Line 120
  (`uses = arguments.TryReadSpace() ? arguments.ReadCreateUseList() : null;`) is 1 of 2: no `CREATE` has a mailbox name
  followed by something other than a space, for example `CREATE "Drafts"(USE (\Drafts))`. That should answer
  `BAD` (invalid arguments).
- `ImapArguments.ReadSpacedUse(bool)`, `ImapArguments.cs:157`, 90% branch. Line 159
  (`(isSpaceFirst && !TryReadSpace()) || !TryReadByte((byte)'\\')`) is 5 of 6: one outcome is missing. Most likely it is a
  second use in a `USE (...)` list that is not preceded by a space (`(USE (\Drafts\Sent))`), or a use without its
  backslash (`(USE (\Drafts Sent))`). Try both. RFC 6154 section 3 / BL-1510.
- `async ImapSession.AnswerUnlessFaultedAsync`, `ImapSession.Faults.cs:37`, 92.86% branch. The `found.Rule switch` at
  line 55 is 7 of 8. Error (line 57), Drop (58), Delay-then-answer and no rule all have tests, so first find the missing
  outcome from the Cobertura `condition-coverage` of line 55. Candidates: a non-Delay, non-Error, non-Drop rule matching
  at command stage (a `RefuseLogin` rule whose point matches a command), or an `await` that completes synchronously.
  POP3 (BL-1645) and SMTP (BL-1646) have the same gap in the same switch shape. Whoever finds the answer first should
  note it in those tasks' Logs.

## Acceptance criteria

- [ ] A named `Surl.Protocol.Imap.UnitTests` test sends `CREATE` with a name followed directly by `(` and asserts the
      tagged `BAD` reply bytes.
- [ ] Named tests send each `USE` list shape that line 159 lacks and assert the tagged reply bytes.
- [ ] A named test reaches line 55's missing outcome and asserts the reply bytes or connection outcome. If no test can
      reach it (it is compiler-generated and unreachable), the Log says so with the evidence, and the task moves to
      Blocked naming that, rather than adding an exclusion.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary` reports `100 | 100`
      with 0 failing members, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Tests only (test-writer). The expected replies come from RFC 9051 / RFC 6154 as Surl answers them today. A reply that
  looks wrong becomes its own task.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
