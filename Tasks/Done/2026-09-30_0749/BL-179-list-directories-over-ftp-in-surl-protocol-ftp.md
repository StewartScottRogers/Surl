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
completed: 2026-09-30
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

- [x] A fast test replays the directory-URL and `-l` fixtures and asserts surl's replies and
      listing bytes are the ADR's.
- [x] Fast tests cover: each listing command without `--list-directories` (answered as missing);
      a listing of `/` leaving out `.surl` and dot-files; `--serve-dot-files` showing dot-files;
      a file name holding a space and a non-ASCII character; an empty directory; `MLST` of a file.
- [x] `dotnet build Surl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- Delivered (2026-09-30): `FtpListingFormat` (the `LIST`, `NLST` and `MLSD`/`MLST` line forms)
  and `LIST`, `NLST`, `MLSD`, `MLST` in `FtpCommandResponder`. RETR and the listings now share
  one `TransferAsync` (425 checks, open, 150, send, reply). `FEAT` gained ` MLST
  type*;size*;modify*;` as ADR-0052 decision 1 lists it, now that `MLST` exists.
- Fixtures `list-directory` and `name-list-directory` recorded from the pinned Windows build
  with `-FtpData` set to surl's own listing bytes (`Fixtures/README.md`, "Listings (BL-179)").
  `RecordedListingTests` replays them.
- Defaults taken inside ADR-0052 decision 7, where the ADR is silent:
  - `LIST`/`NLST` of a single file name the file by its last segment (`ls -l` style), not the
    path as sent; it is listed whatever `--list-directories` says, as the ADR requires.
  - A missing, hidden, refused or above-root path to `LIST`/`NLST`/`MLSD`, and `MLSD` of a
    file, is `550 No such directory` - the same reply as a directory with listings off, so no
    reply tells a hidden entry from a missing one (ADR-0006 section 2).
  - `LIST`/`NLST` drop each leading word starting with `-` (`LIST -la dir` lists `dir`).
  - "Within the last 180 days" means written no later than now and at most 180 days before
    it; a future time shows the year, as `ls` does.
  - `MLST` names the resolved absolute path, rendered as ADR-0006 section 3 says. A
    directory's `modify` comes from its entry in its parent's listing; `/` (no parent) and a
    directory its parent does not list by that spelling give `type=dir;` alone (RFC 3659 facts
    are optional).
  - A listing whose status cannot be read is 550 with a log note, as `SIZE`/`RETR` already do
    (ADR-0023); curl closing the data connection early is `426` with a note, as for `RETR`.
- Quality: 281 FTP tests pass; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary`
  reports 100% line, 100% branch, 0 failing members (worst CRAP 10).

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl answers LIST, NLST, MLSD and MLST (listings only with --list-directories), so curl ftp://host/dir/ and curl -l work
