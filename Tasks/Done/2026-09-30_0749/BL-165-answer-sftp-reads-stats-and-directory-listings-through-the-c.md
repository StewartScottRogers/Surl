---
id: BL-165
title: Answer SFTP reads, stats and directory listings through the content store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-163, BL-155, BL-232]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-042
created: 2026-09-29
completed: 2026-09-30
---
# BL-165 — Answer SFTP reads, stats and directory listings through the content store

## Goal

The `sftp` subsystem answers SFTP version 3's read side through the content store -
`INIT`, `REALPATH`, `STAT`, `LSTAT`, `FSTAT`, `OPEN` for reading, `READ`, `CLOSE`, `OPENDIR` and
`READDIR` - as BL-155's ADR decides, so `curl sftp://host/file` and `curl sftp://host/dir/`
have a server to talk to.

## Context

- Decision: BL-155's ADR (version, path mapping, attributes, long-name format, status codes and
  messages, handle limits, the SFTP packet bound). Specification: draft-ietf-secsh-filexfer-02.
- Exposure (ADR-0006 section 2, ADR-0015): a listing without `--list-directories` and a hidden
  entry are answered exactly as missing; `/.surl` never (ADR-0031 decision 5); symbolic links
  per `--follow-symlinks`. What a listing holds: ADR-0009.
- Content: `Surl.Content.UnitLibrary/ContentStore.cs`; add the `ProjectReference` if BL-164 did
  not. Tests use `InMemoryContentFileSystem` and a fake `TimeProvider`.
- Plugs into BL-163's channel seam; tested at channel level with spec-derived byte scripts and
  a fixtures `README.md` saying BL-172 proves them against pinned curl (ADR-0003).
- Code to copy (never expectations): the Curl port's `Sftp/` (`SftpAttributes`,
  `SftpPacketType`, `SftpStatusCode`, `SftpDirectoryEntry`), turned to the server's side.

## Acceptance criteria

- [x] Fast tests cover each request in the Goal with its success answer, and: a missing file, a
      hidden file, `/.surl`, a directory listing with and without `--list-directories`, a read
      past the end (`SSH_FX_EOF`), an unknown handle, the handle limit, a packet over the bound,
      and a request type not in the read side (answered as the ADR says until BL-166).
- [x] No status message sent to the peer holds a local path or an exception message (ADR-0006
      section 3).
- [x] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- Built: `SftpSession` (the `ISshChannelHandler` for `sftp`), `SftpChannelFraming`, `SftpRequest`,
  `SftpReply`, `SftpPath`, `SftpHandle`, `SftpPacketType`, `SftpStatusCode`, and
  `SshContentChannelHandlers` (SFTP from a `ContentStore`, SCP still refused until BL-164).
  `SshProtocolServer` gains a public constructor taking a `ContentStore`; `Surl.Console` does not
  compose the SSH server yet, so no `--aihelp` change is due here (BL-171).
- `Surl.Content.UnitLibrary` is now a `ProjectReference` of the SSH library (the task's Context
  says to add it; FTP references it too, ADR-0002's table).
- Seam change: `ISshChannelHandlers.ForScp`/`ForSftp` take the `ExchangeContext`, and
  `SshConnectionProtocol` takes the context instead of the log and `--max-line`, because a handler
  needs the log, the clock and `--max-message`, which exist per connection only.
- Choices inside ADR-0054, taken as sensible defaults:
  - Until BL-166, `OPEN` with any flag but `READ`, and `WRITE`, `SETSTAT`, `FSETSTAT`, `REMOVE`,
    `MKDIR`, `RMDIR`, `RENAME`, are answered as decision 10 answers a type not served:
    `OP_UNSUPPORTED` `Operation unsupported` (note: `only reads are served` for `OPEN`).
    `SYMLINK` and `EXTENDED` already get decision 10's final answers.
  - `READ` on a directory handle is `FAILURE` `Is a directory`; `READDIR` on a file handle is
    `FAILURE` `Not a directory` (both messages in decision 6's table).
  - `READLINK` is served (decision 8) though the Goal does not list it.
  - `CLOSE` of a directory handle notes `listed <n> entries` beside decision 13's `read <n> bytes`.
  - A length above .NET's largest array with `--max-message 0` ends the session as a malformed
    packet; the body buffer grows from 64 KiB as bytes arrive, so a length field alone cannot
    make the server allocate (code review).
  - A `READ` whose copy returns nothing because the file shrank is `EOF`, never an empty `DATA`
    (libssh2 fails a short read not followed by `EOF`; code review).
  - The handle counter's wrap after 2^32 handles in one session is not handled: unreachable in
    practice.
- Tests: `SftpSessionTests` (89 cases) at channel level with hand-built byte scripts, the ADR's
  worked bytes pinned (`VERSION`, `REALPATH .`, `ATTRS` of `a.txt`), and one run through the whole
  server; `Fixtures/README.md` says BL-172 proves them against pinned curl (ADR-0003). Quality:
  100% line and branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The sftp subsystem answers SFTP v3's read side (INIT, REALPATH, STAT, LSTAT, FSTAT, OPEN for reading, READ, CLOSE, OPENDIR, READDIR, READLINK) through the content store
