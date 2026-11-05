---
id: BL-397
title: Play the server half of upstream scp, sftp and smb test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-393]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests, Protocols/FileTransfer/Surl.Protocol.Smb.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Smb.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed:
---
# BL-397 — Play the server half of upstream scp, sftp and smb test cases

## Goal

The upstream test-case run covers cases whose `<server>` is `scp`, `sftp` or `smb`. Surl plays their
server half as BL-386's ADR defines, and the pass-rate file counts them.

## Context

- Decision: BL-386's ADR, decision 2, for these servers. Upstream's SSH and SMB cases mostly serve real
  files rather than a `<reply>` (their commands name `%SCP_PWD`, `%SFTP_PWD` and `%SSHPORT`, and
  FILEFORMAT.md's variables include `%SSHSRVMD5` and `%SSHSRVSHA256`, the server key's hashes). So the
  server half is likely surl's own SSH and SMB servers serving the case's `<file>` sections from the
  content store. The ADR settles it.
- How Surl answers: ADR-0051, ADR-0054 and ADR-0058 (SSH), and ADR-0073 (SMB). The Windows reference
  build has no `smb`, so on Windows SMB cases run with the supplementary build ADR-0030 pinned. The
  runner, checker and pass-rate file are BL-392 and BL-393's.

## Acceptance criteria

- [ ] Unit tests drive the server half with an inline case per server. Each excluded case class gives
      its named skip reason.
- [ ] The run covers every runnable SCP, SFTP and SMB case on Windows. The pass-rate file's rows
      include them, and Notes record the counts per server.
- [ ] Failing cases are grouped by cause into new tasks filed with `task-planner`, with their IDs in
      Notes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
