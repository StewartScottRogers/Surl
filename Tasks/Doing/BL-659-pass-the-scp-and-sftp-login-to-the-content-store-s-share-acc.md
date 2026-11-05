---
id: BL-659
title: Pass the SCP and SFTP login to the content store's share access check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-656]
touches: [Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-659 — Pass the SCP and SFTP login to the content store's share access check

## Goal

The SSH server hands the content store the connection's authenticated user and the scheme (`scp` or `sftp`) on
every file operation, and answers a share-access refusal with the SFTP status and SCP reply BL-642's ADR
measured.

## Context

- Decision: BL-642's ADR decision 2 (`SSH_FX_PERMISSION_DENIED` or what it measured; the SCP error line; curl's
  exit codes, measured against pinned upstream curl 8.21.0). ADR-0054 (how the SSH server answers SCP and SFTP),
  ADR-0051 (user authentication), BL-656's caller API.
- Where: `Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary`.
- Oracle: pinned upstream curl only. `UpstreamCurlTransfersFilesWithSurlOverScpTests`,
  `UpstreamCurlTransfersFilesWithSurlOverSftpTests` and `UpstreamCurlLogsInToSurlOverSshTests` must pass unchanged.

## Acceptance criteria

- [ ] Named tests replay SFTP and SCP requests where: an `r` user reads and lists a share but a write is refused
      with the ADR's status or reply; an `rw` user writes; a user with no access is refused; with no access
      configured every existing test passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
