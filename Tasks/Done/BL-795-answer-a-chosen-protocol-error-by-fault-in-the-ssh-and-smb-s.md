---
id: BL-795
title: Answer a chosen protocol error by fault in the SSH and SMB servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests, Protocols/FileTransfer/Surl.Protocol.Smb.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Smb.UnitTests, Documentation/Planning/Decisions/ADR-0135-how-the-ssh-and-smb-servers-answer-a-chosen-error.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-05
completed: 2026-11-05
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

- [x] Named tests: an SFTP open of `/up/a` under a rule gets `SSH_FXP_STATUS` with the rule's code and message and
      the content store is unchanged, while `/b` opens; an SCP upload under a rule gets SCP's error line; an SMB
      `CREATE` under a rule gets the rule's NTSTATUS; each publishes one `FaultAppliedEvent`.
- [x] A named test shows an SFTP rule scoped to account `alice` fires only for `alice`.
- [x] With no rule, every existing test of both libraries passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- **Built.** `SshCommandFaults` (SFTP per request in `SftpSession`, SCP per command in both handlers) and
  `SmbCommandFaults` (per request after the session setup in `SmbSession`). Decisions in ADR-0135, which follows
  the HTTP lane's ADR-0134 decisions 4 to 6 so the families agree: a reply that does not fit, or a `delay`/`drop`
  rule at a command, is noted and not applied; one `FaultAppliedEvent` per application, target the path or else
  the command; the point carries the listen URL formatted as the engine formats it.
- **Choices.** SCP sends the reply exactly after byte 1 (no `scp: ` prefix); an SCP upload asks before its first
  `\0`, a download after the client's start `\0`. SFTP handle requests are matched on the handle's path. SMB
  commands are named `TREE_CONNECT`, `CREATE`, `READ`, `WRITE`, `CLOSE`, `TREE_DISCONNECT`; the fault response
  sets `SMB_FLAGS2_NT_STATUS` so its status field says it holds an NTSTATUS.
- **Tests.** `SshCommandFaultsTests` (the acceptance's SFTP `/up/a` vs `/b`, `alice` vs `bob`, SCP upload and
  download, READ on a handle, no-path cases, misfitting replies, other kinds) and `SmbCommandFaultsTests`
  (`CREATE` under `C0000022` vs another file, `READ`, every command's name and path, misfits, other kinds).
  `ScriptedFaultPlan` matches paths exactly, so the tests name `/up/a`; the engine's `FaultPlan` matches `/up` as
  a segment prefix. SSH 1090 and SMB 222 tests green; both libraries 100% line and branch (cobertura).
- **Upstream curl exit codes.** Not measured for the fault itself: a live `surl` does not apply faults until
  BL-803 composes the plan, so the exchange cannot be run yet; filed BL-902 to measure them. The same bytes as
  natural refusals are already measured with pinned 8.21.0 in `Surl.Conformance.UnitTests`: SFTP status 2 exits
  78, 3 exits 9, 4 exits 79; an SCP error line exits 78 on a download and 25 on an upload; an SMB `CREATE`
  refusal exits 78 (9 only for `ERRDOS/ERRnoaccess`). Not yet seen: an SCP upload refused before its first
  `\0`, and an NTSTATUS-flagged SMB status.
- **Touches widened.** Added ADR-0135 and `Documentation/Planning/Decisions/README.md` for the decision record;
  no task in Doing names either.
- **Follow-ups filed.** BL-901 (command-stage delay and drop in SSH and SMB), BL-902 (live exit codes, after BL-803).

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. SFTP, SCP and SMB answer a matching chosen-error fault rule with SSH_FXP_STATUS, byte 1 and the rule's line, or the rule's NTSTATUS, publishing FaultAppliedEvent
