---
id: BL-1535
title: Name the out-of-space rule in the FTP and TFTP refusal notes (ADR-0129 amendment 1)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1534]
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1535 — Name the out-of-space rule in the FTP and TFTP refusal notes (ADR-0129 amendment 1)

## Goal

FTP and TFTP exchange-log notes for an upload refused by an out-of-space rule name the rule instead of a limit.

## Context

ADR-0129 amendment 1 (BL-1524) decides this; read it first. The wire bytes do not change.

## Acceptance criteria

- [ ] FTP note, when RefusedByFaultRuleId is set, is exactly: The upload was refused by the fault rule <id> (out-of-space); the partial upload was deleted and the data connection was reset.
- [ ] TFTP note, when set, is exactly: The write was refused by the fault rule <id> (out-of-space); the transfer was ended with an error.
- [ ] With it null both notes are unchanged, and the 552 reply and TFTP ERROR 3 bytes are byte for byte unchanged (tests pin both).
- [ ] Both libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
