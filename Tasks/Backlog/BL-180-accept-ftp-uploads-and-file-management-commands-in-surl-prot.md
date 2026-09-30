---
id: BL-180
title: Accept FTP uploads and file-management commands in Surl.Protocol.Ftp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-178, BL-226]
touches: [Surl.Protocol.Ftp.UnitLibrary, Surl.Protocol.Ftp.UnitTests]
requirement: FR-036
created: 2026-09-29
completed:
---
# BL-180 — Accept FTP uploads and file-management commands in Surl.Protocol.Ftp

## Goal

`FtpProtocolServer` accepts `STOR` and `APPE` (and resumed uploads) into the content store and
answers the file-management commands curl sends (`MKD`, `RMD`, `DELE`, `RNFR`/`RNTO`, and the
`SITE` forms BL-173's ADR decides), each only for a logged-in user with `--allow-uploads`.

## Context

- Decisions: BL-173's ADR (replies, what the store cannot do); ADR-0006 sections 2 and 5
  (writes refused with `550` without `--allow-uploads`; an upload past `--max-filesize`
  answered `552` with the partial upload deleted); ADR-0031 decision 5 (nothing under `/.surl`);
  ADR-0032 (writes need a login).
- Content: `ContentStore`'s upload path (temporary dot-file, then rename, BL-086). If a
  command the ADR requires (a rename, a delete, a directory removal) has no `ContentStore`
  member yet, do not add it here: stop, have `task-planner` file the `Surl.Content` task, add it
  to `depends-on` and move this task to Blocked.
- Fixtures: BL-173's recordings of `-T`, `-C -` upload, `--ftp-create-dirs -T` and the `-Q`
  commands.

## Acceptance criteria

- [ ] A fast test replays each fixture named in Context and asserts surl's replies are the
      ADR's and the uploaded bytes land in the in-memory store.
- [ ] Fast tests cover: each write without `--allow-uploads` (`550`); an upload past
      `--max-filesize` (`552`, nothing left behind); `APPE` to a missing file; `RNTO` without
      `RNFR`; `DELE` of a missing file; `MKD` of an existing directory; any write under `/.surl`.
- [ ] `dotnet build Surl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
