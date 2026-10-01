---
id: BL-298
title: Accept SMB file writes under --allow-uploads in Surl.Protocol.Smb
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-297]
touches: [Surl.Protocol.Smb.UnitLibrary, Surl.Protocol.Smb.UnitTests]
requirement: FR-050
created: 2026-09-30
completed: 2026-09-30
---
# BL-298 — Accept SMB file writes under --allow-uploads in Surl.Protocol.Smb

## Goal

`Surl.Protocol.Smb` answers upstream curl's `SMB_COM_NT_CREATE_ANDX` for writing and
`SMB_COM_WRITE_ANDX`, storing `curl -T file smb://host/share/name` through the content store only
with `--allow-uploads`, bounded by `--max-filesize`, as BL-283's ADR decides.

## Context

- Decision: BL-283's ADR (the upload statuses, and which curl exit - `CURLE_UPLOAD_FAILED` (25) or
  access denied (9) - each produces), ADR-0006 section 2 (uploads off by default), ADR-0015 (refused
  or capped, the partial file deleted).
- Content: `ContentStore`'s upload session (`ContentUploadSession`, `ContentUploadOpening`,
  `ContentUploadResult`), and the random-access upload session BL-233 added if writes arrive at
  offsets.
- Fixtures: the ADR's upload cases recorded with the static-curl 8.21.0 Windows build.

## Acceptance criteria

- [x] Tests in `Surl.Protocol.Smb.UnitTests` replay a recorded upload: stored byte for byte with
      uploads allowed; refused with the ADR's status without `--allow-uploads`; a file past
      `--max-filesize` refused and its partial file deleted; no test opens a socket.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Smb.UnitLibrary`.

## Notes

- Built ADR-0073 decision 5's uploads in `SmbSession`: an NT create asking to write (write access,
  or a disposition other than `FILE_OPEN`) opens `ContentStore.OpenUploadAsync` with an opening that
  starts empty, creates a missing file and replaces an existing one (`FILE_OVERWRITE_IF`), answered
  `FILE_CREATED` (2) or `FILE_OVERWRITTEN` (3, a visible file was there), all four times the injected
  clock's now, length 0. Each write is `WriteAtAsync`, answered with exactly the count it carried; the
  close commits; a tree disconnect or the connection's end discards an open upload (`SmbSession` is
  now `IAsyncDisposable`, disposed by `SmbProtocolServer` however the exchange ends).
- Choices the ADR left open, taken by its rules: a name the path rules refuse is `ERRDOS/ERRbadfile`
  for an upload as for a read (decision 2, "answered as absent"); after a write past `--max-filesize`
  (`ERRHRD/ERRdiskfull`) or a store write failure (`ERRHRD/ERRgeneral`), later writes get the same
  status and the close is answered with success, noted `SMB close <name>: upload discarded`; a commit
  that finds a directory at the name is `ERRDOS/ERRnoaccess`, one that throws `ERRHRD/ERRgeneral`; a
  write offset that is negative or would overflow is `ERRSRV/ERRerror` (malformed).
- Recorded four fixtures with the static-curl 8.21.0 pin and `Record-CurlExchange.ps1 -Raw`, fed the
  exact bytes the server sends: `upload-small` (0), `upload-large` (0; writes of 32767 and 7233),
  `upload-refused` (9, uploads off), `upload-too-large` (25, `--max-filesize 1000`), matching
  ADR-0073 rows 17 to 20. No script extension was needed.
- `Measure-CodeQuality.ps1`: `Surl.Protocol.Smb.UnitLibrary` 100% line, 100% branch, 0 failing
  members (worst CRAP 8). `ServeSessionAsync` awaits the exchange with
  `ConfigureAwaitOptions.SuppressThrowing` before disposing the session, rather than in a `finally`,
  so the compiler generates no rethrow branch that no test can take.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Protocol.Smb stores curl -T uploads through the content store under --allow-uploads, refuses them without it (curl 9) and past --max-filesize (curl 25, partial file deleted)
