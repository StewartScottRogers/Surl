---
id: BL-1367
title: Cover the last FTP upstream test case reply kind
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1367 — Cover the last FTP upstream test case reply kind

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 100% line, 99.83% branch, 1 failing member.
- `FtpCommandResponder.AnswerWithoutDataConnectionAsync` (async) -
  `Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary/FtpCommandResponder.UpstreamTestCase.cs:93`, 83.33% branch;
  the `return kind switch` at line 96 is 5 of 6: one reply kind (or the switch's default arm) is never answered.

## Acceptance criteria

- [ ] A named `Surl.Protocol.Ftp.UnitTests` test answers an upstream test case command of the missing kind (identify it
      from line 96's arms) and asserts the reply bytes; if the missing edge is a default no kind can reach, reshape the
      switch so it has none and say so in the Notes.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary` reports `100 | 100` with 0 failing members, and
      the fast tests are green.

## Notes

- Tests first (test-writer). Expected bytes come from the upstream test case data, never from the Curl port.

## Log

- 2026-11-17: Created.
