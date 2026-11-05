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
completed: 2026-11-05
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

- [x] Named tests replay SFTP and SCP requests where: an `r` user reads and lists a share but a write is refused
      with the ADR's status or reply; an `rw` user writes; a user with no access is refused; with no access
      configured every existing test passes unchanged.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Plan (follows ADR-0102 decisions 2.2 and 2.3 and BL-656's API; no new ADR needed):
  `SshUserAuthentication.AccountName` keeps the account the policy named on an accepted login
  (null when `--allow-anonymous` accepted it unchecked). `SshConnectionProtocol` hands it to
  `ISshChannelHandlers.ForScp`/`ForSftp`; `SshContentChannelHandlers` turns it into a
  `ContentCaller` on `scp` or `sftp`, and every path goes through
  `ContentStore.MapRequestPathForCaller`.
- Replies need no change: a share the caller may not read maps as absent (SFTP `NO_SUCH_FILE`,
  SCP `No such file or directory`); a write in an `r` share is the store's `NotPermitted`,
  already answered SFTP `PERMISSION_DENIED` and SCP `Permission denied` - the replies ADR-0102
  2.3's table names, measured in ADR-0054.
- Default chosen: an empty account name is treated as anonymous, so `ContentCaller.ForAccount`
  can never throw on a policy that names "" (secure by default).
- Behaviour change only with shares: the SSH lookups now name their scheme, so a share's
  `protocols=` admitting `scp` or `sftp` is now visible to them (before, the scheme-less lookup
  saw only shares open to every scheme). With no shares and no access configured, every
  existing test passes unchanged.
- Tests: `SshContentChannelHandlersTests` (r reads/lists, write refused; rw uploads; carol,
  unchecked and empty logins find the share missing; SFTP and SCP each) and
  `SshConnectionProtocolTests.Subsystem_AfterAPasswordLogin_IsHandedTheAccountThePolicyNamed`.
- Verified: `dotnet build -warnaserror` clean, fast tests green (62 test projects),
  Surl.Protocol.Ssh.UnitLibrary 100% line and branch (cobertura from the MSTest collector).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. SCP and SFTP pass the login's account and scheme to the content store: r reads and lists, rw writes, no access finds the share missing
