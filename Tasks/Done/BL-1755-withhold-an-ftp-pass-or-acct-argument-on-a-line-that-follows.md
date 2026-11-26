---
id: BL-1755
title: Withhold an FTP PASS or ACCT argument on a line that follows a bare line feed
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1755 — Withhold an FTP PASS or ACCT argument on a line that follows a bare line feed

## Goal

The FTP exchange log writes the argument as `****` on a `PASS` or `ACCT` line when the client ends lines with a bare
line feed.

## Context

- Found while working BL-1752. `FtpLineReader` ends a command line at LF, but `LineBytesRedactor` ends one only at
  CR LF, so `USER alice<LF>PASS <password><CR><LF>` reaches `FtpLineRedactionRule` as one line starting `USER` and is
  logged with the password. Read from the code; no test reproduces it yet.
- ADR-0182 decisions 1 and 6 set the rule; amendment 6, point 3, judges each bare-LF line in a SOCKS tunnel.
- `FtpLineRedactionRule` can judge each LF-separated piece of the line it is handed, with no change to the shared
  `LineBytesRedactor`.

## Acceptance criteria

- [x] A named test in `Surl.Protocol.Ftp.UnitTests` shows the password masked on
      `USER alice<LF>PASS <password><CR><LF>`.
- [x] A named test shows every other line of the same chunk logged as sent, line feeds included.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- `FtpLineRedactionRule.Redact` splits the line it is handed at each LF and judges each piece as one command line, as
  `FtpLineReader` reads it. `LineBytesRedactor` is unchanged. No new ADR: this applies ADR-0182 decisions 1 and 6 to
  every line the FTP server reads.
- Tests, in `FtpExchangeLogRedactionTests`: `Pass_AfterABareLineFeed_IsMasked`,
  `EveryOtherLineOfTheSameChunk_IsLoggedAsSentLineFeedsIncluded`,
  `Pass_EndedByABareLineFeed_KeepsTheLineFeedAndWhatFollows` (before the fix the mask swallowed the following lines)
  and `PassWithNoArgument_EndedByABareLineFeed_IsLoggedAsSent`. All four failed before the fix.
- A client that never sends CR LF still has its bytes held until the exchange ends, or masked whole past 64 KiB
  (ADR-0182 decision 6). That loses log detail and reveals no secret.
- Verified: `dotnet build -warnaserror` clean, `dotnet format --verify-no-changes` clean, fast tests green,
  `Measure-CodeQuality.ps1` on the FTP tests' coverage reports 0 failing members in `Surl.Protocol.Ftp.UnitLibrary`.
- Filed BL-1756: the NATS redaction rule has the same bare-LF gap (read from the code).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The FTP exchange log masks PASS and ACCT arguments on lines ended by a bare line feed
