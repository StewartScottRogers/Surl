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
completed: 2026-11-05
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

- [x] Unit tests drive the server half with an inline case per server. Each excluded case class gives
      its named skip reason.
- [x] The run covers every runnable SCP, SFTP and SMB case on Windows. The pass-rate file's rows
      include them, and Notes record the counts per server.
- [x] Failing cases are grouped by cause into new tasks filed with `task-planner`, with their IDs in
      Notes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps
      100% line and branch coverage.

## Notes

- **Result (Windows, 2026-11-05).** Full run: 964 runnable, 895 pass, 69 fail, 1,049 skipped (was
  923 / 854 / 69 / 1,090); no regressions. Per server: SFTP 24 ran, 24 pass; SCP 14 ran, 14 pass;
  SMB 2 ran, 2 pass (1451, 1850); 2004 (TFTP, SFTP and SCP in turn) passes. The 20 other cases naming
  `scp` or `sftp` skip with their named reasons: `precheck` 9 (610, 611, 613, 614, 615, 638, 639,
  1446, 1583), `postcheck` 5 (608, 612, 624, 625, 627), `libtest` 2 (582, 583), `unittest` 2 (2604,
  2605), `server-not-built:http-proxy` 1 (445), `no-server-half` 1 (632). The suite took under
  2 minutes.
- **No failing cases**, so no failure tasks were filed: every runnable SCP, SFTP and SMB case passes.
- **How the server half is played (ADR-0080 decision 2, "file system").** Surl's own
  `SshProtocolServer` and `SmbProtocolServer`, composed in `Surl.Conformance.UnitTests` by
  `UpstreamTestCaseFileServers` beside the existing halves; no change to either protocol library was
  needed, so their projects are untouched. Choices, each the simplest faithful mate of upstream's
  runtests setup:
  - The SSH root is the case's folder, so `%SFTP_PWD` and `%SCP_PWD` are empty: the URL path
    `/log/file` names the same file upstream's absolute path does.
  - Logins follow `tests/sshserver.pl`'s sshd: `%USER` with the case's client key only; `none`,
    passwords, other keys and users refused (`UpstreamTestCaseSshLoginPolicy`). Accept-all made 606
    and 656 fail (curl let in where upstream's sshd refuses).
  - `%USER` is the run's user name without blanks or other characters an unquoted `-u %USER:`
    would split ("Stewart Rogers" is `StewartRogers`); only SCP and SFTP cases name `%USER`.
  - Host key: one RSA key per run; `%SSHSRVMD5` lower-case hex, `%SSHSRVSHA256` base64 without
    padding (as `ssh-keygen -l` gives it), `%SSHKEYALGO` `ssh-rsa`. Client key: one RSA pair per run,
    PKCS #1 PEM and an OpenSSH line, the forms `SshTestKeys` already proved every pinned libssh2 reads.
  - `<upload>` for SSH is checked against `%LOGDIR/upload.<N>`, the file runtests reads and the
    upload lands in.
  - SMB: a `TESTS` share holding the case's `<data>` as file `<N>`, as upstream's `smbserver.py`
    answers. smb cases run with `LocateForProtocol(..., "smb")`'s build, which on Windows is the
    static 8.21.0 supplementary build ADR-0030 pinned for SMB, selected under its own features.
- These are test-harness choices inside ADR-0080 decision 2, recorded here and in
  `Surl.Conformance.UnitLibrary/CLAUDE.md`, not a new ADR.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl plays the server half of every runnable upstream scp, sftp and smb case; all 41 pass and Windows rises to 895 of 964
