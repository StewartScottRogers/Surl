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
completed:
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

- [ ] Named tests replay sessions where: an `r` account retrieves and lists a share but `STOR` is refused with the
      ADR's reply; an `rw` account stores; an account with no access gets the ADR's reply on `RETR` and `LIST`;
      with no access configured every existing test passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
