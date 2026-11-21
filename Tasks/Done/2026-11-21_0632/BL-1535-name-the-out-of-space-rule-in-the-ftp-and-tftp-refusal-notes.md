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
completed: 2026-11-21
---
# BL-1535 — Name the out-of-space rule in the FTP and TFTP refusal notes (ADR-0129 amendment 1)

## Goal

FTP and TFTP exchange-log notes for an upload refused by an out-of-space rule name the rule instead of a limit.

## Context

ADR-0129 amendment 1 (BL-1524) decides this; read it first. The wire bytes do not change.

## Acceptance criteria

- [x] FTP note, when RefusedByFaultRuleId is set, is exactly: The upload was refused by the fault rule <id> (out-of-space); the partial upload was deleted and the data connection was reset.
- [x] TFTP note, when set, is exactly: The write was refused by the fault rule <id> (out-of-space); the transfer was ended with an error.
- [x] With it null both notes are unchanged, and the 552 reply and TFTP ERROR 3 bytes are byte for byte unchanged (tests pin both).
- [x] Both libraries keep 100% line and branch coverage.

## Notes

- TFTP note omits the request label prefix, as the ADR and criterion give the sentence exactly. The caller used for the mapping is now passed to the write transfer; FTP reads the same caller it mapped with. With an out-of-space rule the store refuses before the data connection opens (FTP: no 150, TFTP: ERROR 3 replaces ACK 0); the 552 and ERROR 3 bytes are unchanged.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. FTP and TFTP out-of-space refusal notes name the rule; tests pin notes and unchanged wire bytes
