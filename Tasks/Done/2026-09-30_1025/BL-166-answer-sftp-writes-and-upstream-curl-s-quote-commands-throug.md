---
id: BL-166
title: Answer SFTP writes and upstream curl's quote commands through the content store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-165, BL-232, BL-233]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-042
created: 2026-09-29
completed: 2026-09-30
---
# BL-166 — Answer SFTP writes and upstream curl's quote commands through the content store

## Goal

The `sftp` subsystem answers SFTP's write side and the requests behind curl's `-Q` commands -
`OPEN` for writing (create, truncate, append), `WRITE`, `REMOVE`, `RENAME`, `MKDIR`, `RMDIR`,
`SETSTAT`, `FSETSTAT`, `SYMLINK`, `READLINK` and any extension BL-155's ADR decides - through
the content store, so `curl -T`, `--append`, `-C -`, `--ftp-create-dirs` and `-Q` work over
`sftp://`.

## Context

- Decision: BL-155's ADR (write semantics against `ContentStore`'s temporary-file-and-rename
  upload, the `-Q` mapping, what the store cannot do and how that is answered).
- Exposure and limits (ADR-0006 sections 2 and 5): every write needs `--allow-uploads` and is
  refused with the ADR's "not permitted" status without it; `--max-filesize` bounds a file,
  and a partial upload over it is deleted; `/.surl` and anything under it refused
  (ADR-0031 decision 5); a link leaving the served root refused whatever the options.
- Content: `Surl.Content.UnitLibrary/ContentStore.cs`. If a write the ADR requires (a rename, a
  directory removal, an append) has no `ContentStore` member yet, do not add it here: this
  task's touches are `Surl.Protocol.Ssh` only. Stop, have `task-planner` file the
  `Surl.Content` task, add it to `depends-on`, and move this task to Blocked.
- Tested at channel level with spec-derived byte scripts, as BL-165.

## Acceptance criteria

- [x] Fast tests cover each request in the Goal with its success answer, and: each write
      without `--allow-uploads`; a write past `--max-filesize` with nothing left behind; an
      append and a write at an offset (resume); a rename onto an existing file; removing a
      missing file; `MKDIR` of an existing directory; a `SETSTAT` the store cannot honour;
      a write into `/.surl`.
- [x] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- Built directly from ADR-0054 decisions 9 to 11 and 13 (fully specified there, so no separate
  architect plan): `SftpSession.Writes.cs` (a `partial` of `SftpSession`, keeping the read side
  and the write side each readable), `SftpAttributes` (the `ATTRS` a client sends, replacing
  `SftpRequest.SkipAttributes` with `ReadAttributes`), and `SftpHandle` gaining the upload, its
  `APPEND`/`READ` flags, the time an `FSETSTAT` defers to `CLOSE`, and why it was discarded.
  `READLINK` was already served by BL-165; `SYMLINK` and every `EXTENDED` keep decision 10's
  `OP_UNSUPPORTED` (no extension is decided), and the `-Q` test drives each of decision 11's rows.
- Choices inside ADR-0054, taken as sensible defaults:
  - `OPEN` without `WRITE` but with `CREAT`, `TRUNC`, `APPEND`, `EXCL` or an undefined bit is
    `OP_UNSUPPORTED` `Operation unsupported` (note: `flags beside READ without WRITE`): those
    flags mean nothing for a read, and the ADR decides only `OPEN` with `WRITE`'s flags.
  - `OPEN` with `WRITE` checks, in order: `--allow-uploads`, a bit above `EXCL`
    (`OP_UNSUPPORTED`), `TRUNC`/`EXCL` without `CREAT` (`BAD_MESSAGE`), the handle limit (before
    the store opens anything, so no upload is opened only to be refused), then the store's answer.
  - `SETSTAT` applies `SIZE` before `ACMODTIME`: a `SIZE` commits a new file over the old one,
    which would lose a time set first. It is therefore not atomic if the time then fails to set
    (code review); a store failure there is `Write failed`.
  - The permissions note (`permissions <octal> not kept`) is written for an `OPEN` that creates a
    file (nothing was there before) and for a `MKDIR`, whenever `PERMISSIONS` came with them.
  - A store failure answers `Write failed` for `WRITE`, `SETSTAT`, `FSETSTAT`, `REMOVE`, `MKDIR`,
    `RMDIR`, `RENAME` and `CLOSE`, `Read failed` otherwise; a failure while an `OPEN` with `WRITE`
    stats its target (before the upload opens) is still `Read failed` (code review, left as is:
    nothing was written yet).
  - Commits run with `CancellationToken.None`: a rename cut off half way would leave the
    temporary file behind. The session's end discards every upload still standing, however it
    ends (cancellation included), one failing discard not keeping the others back (code review).
  - A `WRITE` whose offset plus length passes `long.MaxValue`, and a `SIZE` above it, are
    `FAILURE` `File too large` (no file could reach it).
- Tests: `SftpSessionTests.Writes.cs` (`SftpSessionTests` now 166 cases) at channel level with hand-built byte scripts,
  plus `UnitTestChangeFailingContentFileSystem`. BL-172 proves them against the pinned build
  (ADR-0003). Quality: 100% line and branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The sftp subsystem answers SFTP's write side (OPEN for writing, WRITE, CLOSE commit, REMOVE, RENAME, MKDIR, RMDIR, SETSTAT, FSETSTAT) and curl's -Q commands through the content store
