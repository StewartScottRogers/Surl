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
completed:
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

- [ ] Tests in `Surl.Protocol.Smb.UnitTests` replay a recorded upload: stored byte for byte with
      uploads allowed; refused with the ADR's status without `--allow-uploads`; a file past
      `--max-filesize` refused and its partial file deleted; no test opens a socket.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Smb.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
