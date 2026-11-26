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
completed:
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

- [ ] A named test in `Surl.Protocol.Ftp.UnitTests` shows the password masked on
      `USER alice<LF>PASS <password><CR><LF>`.
- [ ] A named test shows every other line of the same chunk logged as sent, line feeds included.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
