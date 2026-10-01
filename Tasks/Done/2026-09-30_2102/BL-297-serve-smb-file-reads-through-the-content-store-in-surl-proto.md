---
id: BL-297
title: Serve SMB file reads through the content store in Surl.Protocol.Smb
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-296]
touches: [Surl.Protocol.Smb.UnitLibrary, Surl.Protocol.Smb.UnitTests]
requirement: FR-050
created: 2026-09-30
completed: 2026-09-30
---
# BL-297 — Serve SMB file reads through the content store in Surl.Protocol.Smb

## Goal

`Surl.Protocol.Smb` answers upstream curl's `SMB_COM_NT_CREATE_ANDX`, `SMB_COM_READ_ANDX` and
`SMB_COM_CLOSE` for reads, serving files from the content store, so `curl smb://host/share/file`
downloads the file's bytes, as BL-283's ADR decides.

## Context

- Decision: BL-283's ADR (the share to content-store mapping, the NT create response's fields - file
  ID, size, attributes, times - and each refusal's status for a missing file, a hidden path, a
  directory and a path outside the share, in the form curl reads: `0x00050001` as access denied,
  curl exit 9; any other non-zero status as not found, curl exit 78).
- Content: `Surl.Content.UnitLibrary/ContentStore.cs` with `ContentPathMapping` and
  `RequestPathSegments` (an SMB path uses backslashes; map it as the ADR says), and the exposure
  defaults (ADR-0006 section 2, ADR-0015: dot-files and hidden paths answered as absent). The csproj
  gains the `Surl.Content.UnitLibrary` reference, which ADR-0002's table allows.
- Reads come 32 KiB at a time (curl's `MAX_PAYLOAD_SIZE`); cover an offset past the end, a file ID
  that is not open, a close of an unknown ID, and the ADR's bound on open files per session.
- Fixtures: the ADR's download cases recorded with the static-curl 8.21.0 Windows build, including a
  file larger than 32 KiB.

## Acceptance criteria

- [x] Tests in `Surl.Protocol.Smb.UnitTests` replay the recorded download of a small file and of one
      larger than 32 KiB over an `InMemoryContentFileSystem` and send the file's bytes in the ADR's
      responses; each refusal in Context answers with the ADR's status; no test opens a socket.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Smb.UnitLibrary`.

## Notes

- Built ADR-0073 decision 5's reads and decision 2's file mapping and bound in `SmbSession`, with
  `SmbOpenFile` holding each FID's tree, mapping, length at open and bytes read. FIDs count from
  1 (lowest free); a tree disconnect forgets its tree's FIDs.
- Recorded `download-small` (10 bytes) and `download-large` (40000 bytes, two reads) with the
  static-curl 8.21.0 Windows pin through `Record-CurlExchange.ps1 -Raw`, fed the exact bytes
  `SmbProtocolServer` sends: curl exited 0 both times, stdout equal to the served file, and its
  requests equal the layouts `SmbTestExchange` builds. Port 18447, to keep off other lanes' ports.
- Choice: `MaxCountHigh` (the read's timeout field) is not read, because the negotiate announces
  no `CAP_LARGE_READX` ([MS-SMB] 2.2.4.2.1); with the 61440 cap it could only matter to a client
  asking more than 65535 bytes, which curl (32768) never does.
- Choice: until BL-298 takes uploads, an NT create that asks to write (`GENERIC_WRITE`,
  `GENERIC_ALL`, `FILE_WRITE_DATA`, `FILE_APPEND_DATA`, or any disposition but `FILE_OPEN`) is
  `ERRDOS/ERRnoaccess` - ADR-0073's answer with uploads off - and a write on a file opened for
  reading is `ERRDOS/ERRbadaccess` (decision 4). BL-298 replaces the first with the upload path.
- Choice: a read offset with the top bit of its 64 bits set (negative as a `long`) is past every
  file's end and answers 0 bytes, as an offset at or past the end does.
- A content store `IOException` during a read is `ERRHRD/ERRgeneral` (decision 4), noted
  `SMB read <share>\<name> failed: <message>`; the FID stays open so curl's close is answered.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl smb:// serves file downloads from the content store: NT create, 32 KiB reads and close, with ADR-0073's refusals, replayed from two pinned-curl recordings
