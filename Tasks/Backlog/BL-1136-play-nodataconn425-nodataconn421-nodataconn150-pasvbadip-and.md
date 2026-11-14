---
id: BL-1136
title: Play NODATACONN425, NODATACONN421, NODATACONN150, PASVBADIP and RETRWEIRDO in the FTP upstream test-case server half
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1135]
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests, Conformance/Surl.Conformance.UnitLibrary]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1136 — Play NODATACONN425, NODATACONN421, NODATACONN150, PASVBADIP and RETRWEIRDO in the FTP upstream test-case server half

## Goal

The FTP upstream test-case server half implements `NODATACONN425`, `NODATACONN421`,
`NODATACONN150`, `PASVBADIP` and `RETRWEIRDO` as BL-1135's ADR decides. Upstream tests 1206, 1207,
1208, 270 and 126 then run instead of skipping `servercmd-unsupported:<command>`, and pass on
Windows, Linux and macOS.

## Context

- All five skip on every platform today (CI run 43629075978; local Windows reference run
  2026-11-14 at `5a45202a`).
- Read BL-1135's ADR first; it fixes every byte and every data-connection action. Do not decide a
  byte here; if the ADR leaves one open, stop and file a task to amend it.
- Where: `Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary` - add the five to the command table
  in `FtpUpstreamTestCaseServerCommands` beside `REPLY`, `COUNT`, `RETRNOSIZE` and the rest, and
  apply them where `FtpUpstreamTestCaseServer` answers `PASV`, `PORT`/`EPRT` and `RETR`. Then add
  the five to the `Ftp` set in `Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServerCommands.cs`
  and its test.
- Expected exits: 1206 and 1207 exit 10, 1208 exits 28 (`--max-time %FTPTIME2`, `-P -`); 270 and 126
  succeed with the `<protocol>` and `<stdout>` they list.
- Stand-in: not applicable. The seam is composed only by `Surl.Conformance.UnitTests`.

## Acceptance criteria

- [ ] For each of the five commands, a fast test in `Surl.Protocol.Ftp.UnitTests` replays the
      request bytes of its case over a fake `IConnection` and gets the bytes the ADR names.
- [ ] `UpstreamTestCaseServerCommands` lists the five for the FTP family, and its test pins them.
- [ ] A local Windows run of `UpstreamTestCasesAgainstSurlTests` passes 1206, 1207, 1208, 270 and
      126 and every case already on `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.
- [ ] `dotnet build -warnaserror` is clean for the three projects, the fast tests are green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
