---
id: BL-1950
title: Fix the broken ADR links in the FTP and SSH library notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary/CLAUDE.md, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1950 — Fix the broken ADR links in the FTP and SSH library notes

## Goal

The ADR links in the FTP and SSH libraries' `CLAUDE.md` files resolve.

## Context

Found while working BL-1929 (2026-11-27) by resolving each relative link against its file's folder. These links start
with `../Documentation/`, which resolves under `Protocols/FileTransfer/`. From each library folder the path is
`../../../Documentation/`. Line numbers are as of that date and approximate.

- `Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary/CLAUDE.md`, lines 4 and 6 (ADR-0052, ADR-0059).
- `Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md`, lines 5, 7, 8, 10, 12 and 14 (ADR-0051, ADR-0058,
  ADR-0060, ADR-0061, ADR-0054, ADR-0062).

Link targets only; no prose changes. No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] Every relative link in the FTP library's `CLAUDE.md` resolves to a file in the repository.
- [x] Every relative link in the SSH library's `CLAUDE.md` resolves to a file in the repository.
- [x] Only the link targets in those two files change.

## Notes

- Changed 8 link targets (FTP 2, SSH 6) from `../Documentation/` to `../../../Documentation/`. Each file has no other
  relative links; all 8 resolve to files.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: no failures.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Every relative ADR link in the FTP and SSH library notes now resolves.
