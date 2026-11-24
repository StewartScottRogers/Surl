---
id: BL-1634
title: Cover the AMQP 0-9-1 Start-Ok redactor's response-past-the-end branch
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-1634 — Cover the AMQP 0-9-1 Start-Ok redactor's response-past-the-end branch

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Amqp0.UnitLibrary` reports 100% line and 100% branch with no
failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Amqp0 99.93% line, 99.9% branch,
  1 failing member, from BL-1460 (commit 8c740e74f).
- `Amqp0LoginBytesRedactor.TryLocateStartOkResponse(ReadOnlySpan<byte>, out string, out int, out int)`,
  `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary/Amqp0LoginBytesRedactor.cs:245`, 92.31% line, 87.5%
  branch, Cx 5, CRAP 5.01. Lines 264-265 are uncovered: `if (arguments.Length < end) return false;` at line 263 is never
  true.
- Input that reaches it: a `Connection.Start-Ok` whose client-properties table and mechanism shortstr are whole, but
  whose `response` longstr length field claims more bytes than the method arguments hold.
- The bytes come from the AMQP 0-9-1 specification (ADR-0083: extended servers are judged by their specification).

## Acceptance criteria

- [ ] A named `Surl.Extended.Protocol.Amqp0.UnitTests` test feeds the redactor a Start-Ok whose `response` length runs
      past the frame's end. It asserts what the exchange log then holds: either nothing of the response is shown in
      clear, or it is withheld the way the class's doc comment says an unlocatable response is handled.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Amqp0.UnitLibrary` reports
      `100 | 100` with 0 failing members, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Tests only (test-writer). If the test shows a truncated response leaking credentials into the log, stop and file that
  as a defect task. Do not widen this one.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
