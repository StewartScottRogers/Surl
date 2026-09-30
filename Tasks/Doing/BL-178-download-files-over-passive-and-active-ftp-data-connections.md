---
id: BL-178
title: Download files over passive and active FTP data connections in Surl.Protocol.Ftp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-177, BL-174]
touches: [Surl.Protocol.Ftp.UnitLibrary, Surl.Protocol.Ftp.UnitTests]
requirement: FR-036
created: 2026-09-29
completed:
---
# BL-178 — Download files over passive and active FTP data connections in Surl.Protocol.Ftp

## Goal

`FtpProtocolServer` opens passive (`EPSV`, `PASV`) and active (`EPRT`, `PORT`) data
connections through BL-174's contract and serves downloads - `RETR`, `SIZE`, `MDTM`, `REST`,
`ABOR` and an early close by curl - from the content store, as BL-173's ADR decides.

## Context

- Decisions: BL-173's ADR (reply codes and texts, address rules, `REST` and range behaviour,
  `TYPE A`); ADR-0006 section 2 (hidden entries and `/.surl` answered as missing, `550`);
  ADR-0023 (how a file-system failure is answered, for the HTTP server - follow its spirit and
  the ADR's FTP reply).
- The server never constructs a socket (ADR-0004 rule 2): it asks `ExchangeContext`'s
  data-connection opener (BL-174), and tests use BL-174's in-memory fake.
- Content: `Surl.Content.UnitLibrary/ContentStore.cs` (byte ranges, sizes, modification times);
  dates from the file's time, never from the clock.
- Fixtures: BL-173's recordings of a download, `-I`, `-r 0-9`, `-C -`, `--disable-epsv`,
  `-P -` and `--disable-eprt`, replayed with the fake data connection.

## Acceptance criteria

- [ ] A fast test replays each fixture named in Context and asserts surl's control replies and
      data bytes are the ADR's.
- [ ] Fast tests cover: a missing, a hidden and a `/.surl` file (`550` or the ADR's reply); a
      directory given to `RETR`; `EPRT`/`PORT` naming an address other than the peer's refused;
      the passive accept timing out (`425` or the ADR's reply); curl closing the data connection
      early; `ABOR`; `REST` past the end of the file.
- [ ] `dotnet build Surl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
