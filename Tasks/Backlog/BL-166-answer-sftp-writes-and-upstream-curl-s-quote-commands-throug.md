---
id: BL-166
title: Answer SFTP writes and upstream curl's quote commands through the content store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-165]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-042
created: 2026-09-29
completed:
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

- [ ] Fast tests cover each request in the Goal with its success answer, and: each write
      without `--allow-uploads`; a write past `--max-filesize` with nothing left behind; an
      append and a write at an offset (resume); a rename onto an existing file; removing a
      missing file; `MKDIR` of an existing directory; a `SETSTAT` the store cannot honour;
      a write into `/.surl`.
- [ ] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
