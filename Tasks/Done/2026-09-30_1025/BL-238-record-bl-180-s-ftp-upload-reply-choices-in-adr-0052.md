---
id: BL-238
title: Record BL-180's FTP upload reply choices in ADR-0052
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-180]
touches: [Documentation/Planning/Decisions/ADR-0052-how-the-ftp-server-answers-and-the-ftp-data-connection-seam.md]
requirement: FR-036
created: 2026-09-30
completed: 2026-09-30
---
# BL-238 — Record BL-180's FTP upload reply choices in ADR-0052

## Goal

ADR-0052 decision 8 states every reply `FtpCommandResponder` now sends for uploads and file
management, including the cases BL-180 decided that the ADR left open.

## Context

- BL-180 built decision 8 in `Surl.Protocol.Ftp.UnitLibrary` but could not edit ADR-0052:
  BL-188 held `Documentation/Planning/Decisions` at the time. BL-180's Notes list the choices.
- The choices, as `FtpCommandResponder` answers them (tests in `FtpUploadTests` and
  `FtpFileManagementTests`):
  1. `SITE` is one of decision 8's commands, so without `--allow-uploads` it is `550 Not
     permitted`; with it, `504 SITE <word> is not supported`.
  2. `STOR`/`APPE` to a directory, to `/`, or to a hidden new name: `550 Not permitted`, before
     any data connection is used (the content store refuses before it reads a byte).
  3. `STOR`/`APPE` whose directory is missing, is a file, or is hidden (`/.surl/x` included):
     `553 No such directory` - the hidden directory is answered as absent.
  4. `REST <n>` before `APPE` follows `STOR`'s rule: `n` must equal the file's length (or be 0).
  5. The file system failing an upload: `451 Cannot write the file`; failing a file-management
     command: `451 The change could not be made`; both noted in the log.
  6. `RNTO` of a directory onto an existing file: `553 Cannot rename a directory onto a file`.
     A source gone between `RNFR` and `RNTO`: `550 Not permitted`.
  7. `MKD` answers `257 "<absolute path>" created`, the resolved path, as RFC 959 recommends.

## Acceptance criteria

- [x] ADR-0052 decision 8 states each of the seven choices above, marked as decided in BL-180 by Claude under Stewart's delegation.
- [x] Every reply text decision 8 names matches a constant or literal in `Surl.Protocol.Ftp.UnitLibrary/FtpCommandResponder.cs`.

## Notes

- Filed by BL-180.
- Rewrote decision 8 in place: its intro now names `SITE` among the commands `--allow-uploads` gates, the table gains APPE's REST rule, MKD's absolute path and RNTO's two added replies, the stale "ContentStore has only WriteUploadAsync" bullet now names the BL-226 methods, and a numbered "decided in BL-180" list states the seven choices with why. Decision 1's `SITE` row was updated to match (it said 504 unconditionally).
- Every reply text in decision 8 was checked against a literal or constant in `FtpCommandResponder.cs` (150, 226, 257, 250 x3, 350, 426, 451 x2, 503, 504, 550 x5, 552, 553 x3, 554).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0052 decision 8 states every upload and file-management reply FtpCommandResponder sends, with BL-180's seven choices recorded
