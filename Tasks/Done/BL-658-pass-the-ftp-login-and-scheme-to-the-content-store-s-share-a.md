---
id: BL-658
title: Pass the FTP login and scheme to the content store's share access check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-656]
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-658 — Pass the FTP login and scheme to the content store's share access check

## Goal

The FTP server hands the content store the session's logged-in account and scheme (`ftp` or `ftps`) on every
`RETR`, `LIST`, `NLST`, `SIZE`, `MDTM`, `STOR`, `APPE`, `DELE`, `RNFR`/`RNTO`, `MKD` and `RMD`, and answers a
share-access refusal with the reply BL-642's ADR measured.

## Context

- Decision: BL-642's ADR decision 2 (the FTP reply for a denied read and write, measured against pinned upstream
  curl 8.21.0 with curl's exit code). ADR-0052 (how the FTP server answers), BL-656's caller API.
- Where: `Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary`.
- Oracle: pinned upstream curl only. `UpstreamCurlFetchesFromSurlOverFtpTests`, `UpstreamCurlUploadsToSurlOverFtpTests`
  and `UpstreamCurlLogsInToSurlOverFtpTests` must pass unchanged.

## Acceptance criteria

- [x] Named tests replay sessions where: an `r` account retrieves and lists a share but `STOR` is refused with the
      ADR's reply; an `rw` account stores; an account with no access gets the ADR's reply on `RETR` and `LIST`;
      with no access configured every existing test passes unchanged.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Caller: `FtpCommandResponder` maps every path with `MapRequestPathForCaller`. A checked login
  (`PasswordLoginVerdict.Accepted`) is `ContentCaller.ForAccount(<USER name>, scheme)`; before a login and after
  `AcceptedUnchecked` (`--allow-anonymous`) it is `ContentCaller.Anonymous(scheme)`, as ADR-0102 decision 2.2 says
  (an unchecked login carries no account name). The scheme is the exchange's, `ftp` or `ftps`.
- Replies (ADR-0102 decision 2.3, no new bytes): a share the caller may not read gets the reply a missing path gets
  (`550 No such file` on `RETR`/`SIZE`/`MDTM`/`DELE`, `550 No such directory` on `CWD`/`LIST`/`NLST`); `STOR`/`APPE`
  in an `r` share is `550 Not permitted` before any data connection is used. `DELE`, `RNFR`/`RNTO`, `MKD` and `RMD`
  in an `r` share get the replies the store's `NotPermitted` already maps to. No upstream curl measurement was
  needed: every reply is one ADR-0052 already pinned.
- Tests: `FtpShareAccessTests` (13). Library coverage measured at 100% line and 100% branch.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The FTP server checks share access as the logged-in account (or anonymous) on ftp/ftps: an r share retrieves and lists but STOR is 550 Not permitted, rw stores, no access is the missing-path 550
