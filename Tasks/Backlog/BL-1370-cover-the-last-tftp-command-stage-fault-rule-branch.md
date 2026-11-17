---
id: BL-1370
title: Cover the last TFTP command stage fault rule branch
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1370 — Cover the last TFTP command stage fault rule branch

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Protocol.Tftp.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 100% line, 99.57% branch, 1 failing member.
- `TftpCommandStageFault.FindAndApplyAsync` (async) -
  `Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary/TftpCommandStageFault.cs:32`, 90.91% branch; the
  `switch (rule)` at line 45 is 8 of 9: one rule shape (kind, or kind with a reply that does or does not fit a TFTP
  error) is never matched.

## Acceptance criteria

- [ ] A named `Surl.Protocol.Tftp.UnitTests` test applies a command-stage fault rule of the missing shape (identify it
      from line 45's cases) to a read or write request and asserts the packet sent or the log note; if the missing edge
      is unreachable by construction, reshape the switch so it has none and say so in the Notes.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Protocol.Tftp.UnitLibrary` reports `100 | 100` with 0 failing members, and
      the fast tests are green.

## Notes

- Tests first (test-writer). Expected bytes from RFC 1350.

## Log

- 2026-11-17: Created.
