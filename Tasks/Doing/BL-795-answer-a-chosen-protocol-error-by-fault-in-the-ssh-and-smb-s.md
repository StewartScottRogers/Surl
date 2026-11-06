---
id: BL-795
title: Answer a chosen protocol error by fault in the SSH and SMB servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests, Protocols/FileTransfer/Surl.Protocol.Smb.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-795 — Answer a chosen protocol error by fault in the SSH and SMB servers

## Goal

When a chosen-error fault rule matches, the SFTP subsystem answers the matching request (open, read, write,
remove, rename on a path) with an `SSH_FXP_STATUS` carrying the rule's status code and message, SCP answers with
its error line, and the SMB server answers the matching command with the rule's NTSTATUS, so a test can make one
file operation fail on purpose.

## Context

- Decision: BL-822's ADR decision 4 (what "command" means per protocol; reply checking).
- Contract: BL-823 (`ExchangeContext.Faults`, `FaultAppliedEvent`). Tests use BL-823's scripted fault plan and
  recording publisher.
- Where: `Surl.Protocol.Ssh` (SFTP and SCP inside the encrypted channel; consult the plan per SFTP request or SCP
  command, after login, before the content store) and `Surl.Protocol.Smb` (per SMB command after session setup).
  Existing tests already drive both through their own in-memory transports; reuse them.
- Request bytes from pinned upstream curl 8.21.0 (`Record-CurlExchange.ps1`), never the Curl port (ADR-0003). The
  SSH tests decrypt with the session keys the existing tests already derive. Record in Notes the exit code
  upstream curl 8.21.0 gives for each named case.
- Stand-in: no store change; visible as a `FaultAppliedEvent` and the exchange's bytes.

## Acceptance criteria

- [ ] Named tests: an SFTP open of `/up/a` under a rule gets `SSH_FXP_STATUS` with the rule's code and message and
      the content store is unchanged, while `/b` opens; an SCP upload under a rule gets SCP's error line; an SMB
      `CREATE` under a rule gets the rule's NTSTATUS; each publishes one `FaultAppliedEvent`.
- [ ] A named test shows an SFTP rule scoped to account `alice` fires only for `alice`.
- [ ] With no rule, every existing test of both libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
