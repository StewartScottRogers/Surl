---
id: BL-909
title: Apply command-stage delay and drop faults in the SSH and SMB servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-795]
touches: [Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests, Protocols/FileTransfer/Surl.Protocol.Smb.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-909 — Apply command-stage delay and drop faults in the SSH and SMB servers

## Goal

A `delay` or `drop` fault rule given `stage=command` that matches an SFTP request, an SCP command or an SMB
command holds that command for the rule's milliseconds, or aborts the connection, instead of being noted as not
applied.

## Context

- ADR-0124 decision 4 lists `delay` at `Command` as each server's to apply and `drop` at `Command` as the
  engine's; ADR-0135 decision 5 (BL-795) left both noted as not applied in `SshCommandFaults` and
  `SmbCommandFaults`, as ADR-0134 decision 5 did for HTTP.
- A delay must wait on `ExchangeContext.TimeProvider` with the engine's idle clock paused ("the server's own
  idle and head timeouts do not run during it"); the servers have no handle on that clock today, so this may
  need a seam in `Surl.Protocol.Abstractions` (add it to `touches` then, and check the HTTP family's
  equivalent follow-up from BL-793 first so both use one seam).
- A drop must abort the connection from inside an SSH channel or SMB exchange.

## Acceptance criteria

- [ ] Named tests: a `delay` rule at `stage=command` on SFTP `OPEN` and on SMB `CREATE` holds the answer until a
      `ManualTimeProvider` advances by the rule's milliseconds, and publishes one `FaultAppliedEvent`.
- [ ] Named tests: a `drop` rule at `stage=command` on SFTP `OPEN`, SCP and SMB `CREATE` aborts the connection
      and publishes one `FaultAppliedEvent`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-05: Created.
