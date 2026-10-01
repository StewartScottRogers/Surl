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
completed:
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

- [ ] Tests in `Surl.Protocol.Smb.UnitTests` replay the recorded download of a small file and of one
      larger than 32 KiB over an `InMemoryContentFileSystem` and send the file's bytes in the ADR's
      responses; each refusal in Context answers with the ADR's status; no test opens a socket.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Smb.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
