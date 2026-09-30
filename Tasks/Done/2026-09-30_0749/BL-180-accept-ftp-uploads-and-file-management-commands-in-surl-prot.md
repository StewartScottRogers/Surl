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
completed: 2026-09-30
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

- [x] A fast test replays each fixture named in Context and asserts surl's replies are the
      ADR's and the uploaded bytes land in the in-memory store.
- [x] Fast tests cover: each write without `--allow-uploads` (`550`); an upload past
      `--max-filesize` (`552`, nothing left behind); `APPE` to a missing file; `RNTO` without
      `RNFR`; `DELE` of a missing file; `MKD` of an existing directory; any write under `/.surl`.
- [x] `dotnet build Surl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- Fixtures: BL-173 described but did not commit upload recordings, so this task recorded six
  with the pinned 8.21.0 build (`upload`, `upload-resume`, `upload-create-dirs`,
  `quote-file-management`, `upload-too-large`, `upload-not-permitted`; Fixtures/README.md), each
  fed surl's own reply texts. All ended with ADR-0052's exit codes (0, 0, 0, 0, 70, 25).
  `RecordedUploadTests` replays them.
- Design: BL-226's `ContentStore` members covered every command, so no `Surl.Content` change.
  `DataConnectionUploadStream` opens the data connection (and sends `150`) only at the store's
  first read, so everything the store refuses before reading (uploads off, a hidden new name, a
  directory in the way) is refused before any data connection is used, as decision 8 requires,
  without the FTP server needing the store's internal hidden-path flag.
- Choices ADR-0052 left open, decided under Stewart's delegation: `SITE` without
  `--allow-uploads` is `550 Not permitted` (it is a decision 8 command); STOR to a hidden or
  missing directory (`/.surl/x` included) is `553 No such directory`, to a hidden name or a
  directory `550 Not permitted`; `REST n` before `APPE` follows `STOR`'s rule; file-system
  failures answer `451 Cannot write the file` / `451 The change could not be made` and are
  noted; a directory renamed onto a file is `553 Cannot rename a directory onto a file`; a
  source gone between `RNFR` and `RNTO` is `550 Not permitted`; `MKD` names the resolved
  absolute path in `257`. BL-188 held `Documentation/Planning/Decisions`, so rather than bounce
  this task over an ADR edit, BL-238 was filed to write these into ADR-0052 decision 8.
- Gates: `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary` 100% line, 100% branch,
  188 members, 0 failing, worst CRAP 10; FTP tests 376 passed; solution fast tests green.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. FTP STOR/APPE (with REST resume) upload into the content store, and MKD/RMD/DELE/RNFR/RNTO/SITE answer upstream curl as ADR-0052 decision 8 says, only with --allow-uploads
