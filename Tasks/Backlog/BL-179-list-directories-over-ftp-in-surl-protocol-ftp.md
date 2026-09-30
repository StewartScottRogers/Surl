---
id: BL-179
title: List directories over FTP in Surl.Protocol.Ftp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-178]
touches: [Surl.Protocol.Ftp.UnitLibrary, Surl.Protocol.Ftp.UnitTests]
requirement: FR-036
created: 2026-09-29
completed:
---
# BL-179 — List directories over FTP in Surl.Protocol.Ftp

## Goal

`FtpProtocolServer` answers `LIST`, `NLST`, `MLSD` and `MLST` (and any other listing command
BL-173's ADR lists) over data connections in the ADR's formats, only with
`--list-directories`, so `curl ftp://host/dir/` and `curl -l` work.

## Context

- Decisions: BL-173's ADR (formats, reply codes); ADR-0006 section 2 (a listing without
  `--list-directories` answered exactly as a missing directory); ADR-0009 (what a content-store
  listing holds: hidden entries and `.surl` left out); ADR-0015 (the exposure options).
- Content: `ContentStore`'s listing; times from the entries, formatted with invariant culture.
- Fixtures: BL-173's recordings of a directory URL and `-l`.

## Acceptance criteria

- [ ] A fast test replays the directory-URL and `-l` fixtures and asserts surl's replies and
      listing bytes are the ADR's.
- [ ] Fast tests cover: each listing command without `--list-directories` (answered as missing);
      a listing of `/` leaving out `.surl` and dot-files; `--serve-dot-files` showing dot-files;
      a file name holding a space and a non-ASCII character; an empty directory; `MLST` of a file.
- [ ] `dotnet build Surl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
