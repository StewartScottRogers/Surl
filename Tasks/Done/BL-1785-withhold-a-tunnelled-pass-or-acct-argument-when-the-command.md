---
id: BL-1785
title: Withhold a tunnelled PASS or ACCT argument when the command follows a bare carriage return
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1783]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1785 — Withhold a tunnelled PASS or ACCT argument when the command follows a bare carriage return

## Goal

The SOCKS tunnel's and the HTTP proxy tunnel's exchange log withholds a `PASS` or `ACCT` argument when the command
follows a bare carriage return.

## Context

- Set aside by BL-1772 (ADR-0182 amendment 33, accepted limits), which reads headers after a bare carriage return but
  commands at the line's start only. curl does not send the shape.
- The rule is `TunnelPasswordAndCredentialHeaderLineRule` in `Surl.LineProtocol`. BL-1783 changes the same method.
- Only the levelled log and trace dumps change; the event stream keeps raw bytes (ADR-0182 decision 4).

## Acceptance criteria

- [x] A test in `SocksExchangeLogRedactionTests` logs `"NOOP\rPASS s3cret\r\n"` and `"NOOP\rACCT s3cret\r\n"` with
      `s3cret` as `****`.
- [x] A new ADR-0182 amendment and the rule's doc comment state the behaviour and close the limit of amendment 33.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch coverage.

## Notes

- A command is now read at the line's start and after every bare CR; the line is masked from whichever of the command's and the withheld header's secret begins earlier (ADR-0182 amendment 36, decided by Claude).
- Left as an accepted limit: a command word ended by a CR (`PASS<CR>secret`) is not read as a command, as amendment 30 asks for a space or a tab. curl does not send it.
- Added `Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/CLAUDE.md` to `touches`: it said the command was read at the line's start only. No task in Doing names it.
- Verified: `dotnet build -warnaserror` 0 warnings; fast tests all passed; `dotnet format --verify-no-changes` clean on the three code projects; `Measure-CodeQuality.ps1 -Library Surl.LineProtocol.UnitLibrary` 0 failing members.
- `--aihelp` and manual text is BL-1787, already filed.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The SOCKS and HTTP proxy tunnel logs mask a PASS or ACCT argument that follows a bare carriage return
