---
id: BL-178
title: Download files over passive and active FTP data connections in Surl.Protocol.Ftp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-177, BL-174]
touches: [Surl.Protocol.Ftp.UnitLibrary, Surl.Protocol.Ftp.UnitTests, Record-CurlExchange.ps1]
requirement: FR-036
created: 2026-09-29
completed: 2026-09-29
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

- [x] A fast test replays each fixture named in Context and asserts surl's control replies and
      data bytes are the ADR's.
- [x] Fast tests cover: a missing, a hidden and a `/.surl` file (`550` or the ADR's reply); a
      directory given to `RETR`; `EPRT`/`PORT` naming an address other than the peer's refused;
      the passive accept timing out (`425` or the ADR's reply); curl closing the data connection
      early; `ABOR`; `REST` past the end of the file.
- [x] `dotnet build Surl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- Built: `FtpDataConnections` (EPSV, EPSV 1/2/ALL, PASV, EPRT, PORT, the peer-address and
  port-1024 defences, one data connection per transfer), `FtpActiveTargetParser`,
  `DataConnectionWriteStream`, and SIZE, MDTM, REST, RETR and ABOR in `FtpCommandResponder`.
  FEAT now also lists EPRT, EPSV, MDTM, PASV, REST STREAM and SIZE, which the server answers.
- Fixtures: `download`, `head`, `range-0-9`, `continue-at-auto`, `continue-at-5`,
  `disable-epsv`, `active-eprt`, `active-port`, recorded from pinned curl 8.21.0 with surl's own
  reply texts (Fixtures/README.md). `-C -` writing to stdout sends no REST, so `continue-at-5`
  was recorded as well, to put a REST offset under a fixture.
- `Record-CurlExchange.ps1` added to `touches` (no task in Doing names it): an overridden RETR,
  LIST, NLST, MLSD, STOR or APPE reply starting with 1 now replaces only the 150 and still
  serves the data connection, so a fixture's `150` can carry surl's text. Its help says so.
- Defaults taken, each within ADR-0052:
  - The data connection is opened (accepted or dialled) before `150`, as vsftpd does, so a
    failure is `425` alone, never `150` then `425`; everything else that refuses a RETR
    (`501`, `550`, `554`, `425 Use PASV or PORT first`) is checked before that.
  - `150 Opening data connection for <path as sent> (<bytes to be sent> bytes)`: the path is
    rendered as ADR-0006 section 3 says (`FtpPath.ToReplyText`), and the count is the length
    minus the REST offset, which is what curl reads from a 150 when it has no SIZE.
  - The passive accept and the active connect each wait the head timeout, counted from the
    transfer command rather than from the 229/227 reply; curl connects on reading 229, so the
    difference only lengthens the wait for a client that never connects.
  - A file whose status cannot be read is answered `550`, as a missing file, with a note
    (ADR-0023's rule for HTTP). A file that fails to read after `150` is answered
    `451 Cannot read the file`, with the data connection reset and a note naming the exception;
    ADR-0052 names no reply for it, and 451 is RFC 959's "local error in processing". Any
    other exception is a defect and escapes, as ADR-0023 says.
  - An early close by curl (a failed write, or a failed completion of writes) resets the data
    connection and answers `426`; a REST offset equal to the length sends no bytes, then `226`.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. FtpProtocolServer serves downloads over passive (EPSV, PASV) and active (EPRT, PORT) data connections with SIZE, MDTM, REST, RETR and ABOR, replayed against 8 pinned-curl fixtures
