---
id: BL-1369
title: Cover the SMTP upstream test case SIZE comparison branch
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1369 — Cover the SMTP upstream test case SIZE comparison branch

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 100% line, 99.7% branch, 1 failing member.
- `SmtpSession.AnswerUpstreamTestCaseMailAsync(byte[], MailUpstreamTestCaseReplies)` -
  `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.UpstreamTestCase.cs:54`, 91.67% branch; line 56 is 5 of 6:
  `argument ?? []` and the `SIZE=` parameter lookup miss one outcome - most likely `MAIL FROM` with no argument at all
  (`argument` null).

## Acceptance criteria

- [ ] A named `Surl.Protocol.Smtp.UnitTests` test answers the upstream test case path's `MAIL` with no argument (and, if
      that is not the missing edge, the outcome line 56 lacks), asserting the reply bytes.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary` reports `100 | 100` with 0 failing members, and
      the fast tests are green.

## Notes

- Tests only (test-writer).

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
