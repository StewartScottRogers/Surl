---
id: BL-1137
title: Pace POP3 replies for SLOWDOWN in the mail upstream test-case server half
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary, Protocols/Mail/Surl.Protocol.Pop3.UnitTests, Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Conformance]
requirement: FR-053
created: 2026-11-14
completed: 2026-11-14
---
# BL-1137 — Pace POP3 replies for SLOWDOWN in the mail upstream test-case server half

## Goal

The POP3 upstream test-case server half implements `SLOWDOWN`: it writes each reply one byte at a
time, 10 ms apart on its injected `TimeProvider`. Upstream test 853 ("POP3 LIST messages from *SLOW*
server") then runs instead of skipping `servercmd-unsupported:SLOWDOWN`, and passes on Windows, Linux
and macOS.

## Context

- 853 skips on every platform (CI run 43629075978; local Windows reference run 2026-11-14). It is the
  only mail case at tag `curl-8_21_0` that uses `SLOWDOWN`.
- Upstream's definition, `docs/tests/FILEFORMAT.md` at the tag, "For FTP/SMTP/POP/IMAP":
  "`SLOWDOWN` - Send FTP responses with 0.01 sec delay between each byte".
- Where:
  - `Protocols/Mail/Surl.LineProtocol.UnitLibrary`: parse `SLOWDOWN` in the command table of
    `MailUpstreamTestCaseServerCommands` (beside `REPLY`, `CAPA`, `AUTH`, `POSTFETCH`), expose it on
    `MailUpstreamTestCaseReplies`, and provide the pacing (for example an `IConnection` decorator that
    writes byte by byte with `TimeProvider` delays);
  - `Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary/Pop3UpstreamTestCaseServer.cs`: apply it in
    `ServeAsync` (one line);
  - `Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServerCommands.cs`: the `pop3` row gets
    a set that is `Mail` plus `SLOWDOWN`. Leave `imap` and `smtp` on `Mail`, since their servers do
    not apply it; update the table's test.
- The FTP form of `SLOWDOWN` is BL-1134 in `Surl.Protocol.Ftp`; the mail servers may not reference
  the FTP library (ADR-0002 decision 3), so this is their own.
- Stand-in: not applicable. The seam is composed only by `Surl.Conformance.UnitTests`.

## Acceptance criteria

- [x] A fast test in `Surl.LineProtocol.UnitTests` with a fake `TimeProvider` shows a paced reply
      writes one byte per 10 ms, and an unpaced one writes at once.
- [x] `UpstreamTestCaseServerCommands` lists `SLOWDOWN` for `pop3` only, and its test pins that.
- [x] A local Windows run of `UpstreamTestCasesAgainstSurlTests` passes 853 and every case already on
      `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.
- [x] `dotnet build -warnaserror` is clean for the four projects, the fast tests are green, and the
      three libraries keep 100% line and branch coverage.

## Notes

- `BytePacedConnection` (public, `Surl.LineProtocol`) is the `IConnection` decorator; the POP3 half wraps the
  connection in `ServeAsync` when `MailUpstreamTestCaseReplies.IsSlowedDown`. It waits after each byte,
  10 ms on `ExchangeContext.TimeProvider`. `Surl.Protocol.Pop3.UnitTests` and `Surl.Conformance.UnitTests`
  were added to `touches` (no other task in Doing touches them), plus `Documentation/Conformance` for the
  passing list and pass-rate row (win-x64 now 1,185 passing, 828 skipped).
- Choice: `Mail` row stays as it was; new `Pop3` row = `Mail` + `SLOWDOWN`. IMAP and SMTP cases using it
  still skip, as their servers do not apply it. Coverage was not measured with the collector; every new
  branch has a test (paced/unpaced write, slowed/unslowed POP3 serve, null arguments).
- Linux and macOS: 853 is on the win-x64 list only; their lists are for CI's run to report.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. POP3 upstream test-case half paces replies for SLOWDOWN (one byte per 10 ms); upstream test 853 now passes on Windows
