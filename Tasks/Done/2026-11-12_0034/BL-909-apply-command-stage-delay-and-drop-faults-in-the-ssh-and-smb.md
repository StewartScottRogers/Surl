---
id: BL-909
title: Apply command-stage delay and drop faults in the SSH and SMB servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-795, BL-901, BL-1035]
touches: [Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests, Protocols/FileTransfer/Surl.Protocol.Smb.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-12
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

- [x] Named tests: a `delay` rule at `stage=command` on SFTP `OPEN` and on SMB `CREATE` holds the answer until a
      `ManualTimeProvider` advances by the rule's milliseconds, and publishes one `FaultAppliedEvent`.
- [x] Named tests: a `drop` rule at `stage=command` on SFTP `OPEN`, SCP and SMB `CREATE` aborts the connection
      and publishes one `FaultAppliedEvent`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and
      branch coverage.

## Notes

2026-11-11 (dark factory lane 4), first attempt - returned to Backlog behind BL-1035: `dotnet build` fails with
"A compatible .NET SDK was not found" (global.json pins 10.0.401; only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100
are installed). Nothing can be built or tested until Stewart reinstalls it. Work so far was left uncommitted for the
shift to stash; the plan, so the next run can redo or resume it:

- Seam: none new. BL-901 already added `CommandStageFaults.FindAndApplyAsync` and `ExchangeContext.IdleClock`
  (ADR-0141); both servers call it with the connection to abort, and keep answering `error` in their own shape.
- SMB (written, unbuilt): `SmbCommandFaults.FindErrorAsync` became `FindAndApplyAsync(context, connection, account,
  command, path)`, returning the fitting error rule or the drop rule (null after a delay); its private
  `FormatListenUrl` is gone for `ReportedListenUrl.Format`. `SmbAnswer.Dropped` (`WasDropped`) makes `SmbExchange`
  write nothing and stop reading. Tests `Create_UnderADelayRule_IsHeldUntilTheClockAdvances500Ms_WithTheIdleClockPaused`
  and `Create_UnderADropRule_AbortsTheConnectionWithNoAnswer`; `Rule_OfAnotherKind_...` now RefuseLogin/OutOfSpace.
- SSH (not started): the handlers have no `IConnection`. Plan: add `IConnection Connection { get; }` to
  `ISshChannelDataStream` (the connection the channel rides on), fed `SshSessionChannel` <- `SshConnectionProtocol` <-
  `SshTransportHandshake`'s connection; test channels expose an `InMemoryConnection`. SFTP ends the session on a drop
  (`SftpSessionEndedException`, exit 1); SCP returns 1 with nothing written. Drop the SSH `FormatListenUrl` copy too.

2026-11-12 (dark factory lane 3), second attempt - done. The pinned SDK is back (BL-1035), so the plan above was
redone and built. No new seam: both servers call BL-901's `CommandStageFaults.FindAndApplyAsync`. Decisions are
recorded as ADR-0135 amendment 1 (Decided by Claude under Stewart's delegation). SMB: `SmbCommandFaults.FindAndApplyAsync`,
`SmbAnswer.WasDropped`. SSH: `ISshChannelDataStream.Connection` (from `SshTransportHandshake` through
`SshConnectionProtocol` and `SshSessionChannel`); `SshCommandFaults.FindSftpFaultAsync` / `FindScpFaultAsync` /
`AnswerScpFaultAsync`; an SFTP drop ends the session (exit 1), an SCP drop returns 1 and writes nothing. Both
libraries' `FormatListenUrl` copies are gone for `ReportedListenUrl.Format`. Tests: SMB 221, SSH 1090, full fast
run green; coverage was reasoned from the tests added (each new branch has one), not measured.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. SFTP/SCP/SMB command-stage delay and drop faults are applied, not noted
