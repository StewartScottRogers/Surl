---
id: BL-1134
title: Pace replies for DELAY, SLOWDOWN and SLOWDOWNDATA in the FTP upstream test-case server half
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests, Conformance/Surl.Conformance.UnitLibrary]
requirement: FR-053
created: 2026-11-14
completed: 2026-11-14
---
# BL-1134 — Pace replies for DELAY, SLOWDOWN and SLOWDOWNDATA in the FTP upstream test-case server half

## Goal

The FTP upstream test-case server half implements the three timing server commands FILEFORMAT
documents, `DELAY`, `SLOWDOWN` and `SLOWDOWNDATA`, on its injected `TimeProvider`. Upstream tests
190, 250, 251, 1086 and 1112 then run instead of skipping `servercmd-unsupported:<command>`, and pass
on Windows, Linux and macOS.

## Context

- All five skip on every platform (CI run 43629075978; local Windows reference run 2026-11-14 at
  `5a45202a`).
- Upstream's definitions, `docs/tests/FILEFORMAT.md` at tag `curl-8_21_0`, "For FTP/SMTP/POP/IMAP":
  - "`DELAY [command] [secs]` - Delay responding to this command for the given time" (190:
    `DELAY CWD 60`, `-m 10`, expects exit 28);
  - "`SLOWDOWN` - Send FTP responses with 0.01 sec delay between each byte" (250, 251: directory
    listings that must still succeed);
  - "`SLOWDOWNDATA` - Send FTP responses with 0.01 sec delay between each data byte" (1086 over
    `ftp`, 1112 over `ftps`, `-m 5`, expect exit 28).
- Where: `Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary` - the command table in
  `FtpUpstreamTestCaseServerCommands` (`REPLY`, `COUNT`, `RETRNOSIZE` and the rest are there), and
  the reply and data writers `FtpUpstreamTestCaseServer` uses. Then add the three to the `Ftp` set in
  `Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServerCommands.cs` and its test.
- Delays come from the `TimeProvider` the half is given, never `Thread.Sleep`; the live run uses
  `TimeProvider.System`. POP3's `SLOWDOWN` (test 853) is a separate task in the mail libraries.
- Stand-in: not applicable. The seam is composed only by `Surl.Conformance.UnitTests`. (Surl's
  stand-in delay fault, ADR-0141, is a separate, shipped feature; do not route these through it.)

## Acceptance criteria

- [x] Fast tests with a fake `TimeProvider` show: `DELAY CWD 60` holds the `CWD` reply until 60 s have
      passed; `SLOWDOWN` writes a control reply one byte per 10 ms; `SLOWDOWNDATA` writes data-connection
      bytes one per 10 ms.
- [x] `UpstreamTestCaseServerCommands` lists `DELAY`, `SLOWDOWN` and `SLOWDOWNDATA` for the FTP
      family, and its test pins them.
- [x] A local Windows run of `UpstreamTestCasesAgainstSurlTests` passes 190, 250, 251, 1086 and 1112
      and every case already on `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.
- [x] `dotnet build -warnaserror` is clean for the three projects, the fast tests are green, and both
      libraries keep 100% line and branch coverage.

## Notes

- Decision: `DELAY [command] [secs]` takes whole seconds and holds every matching command (`Task.Delay` on `ExchangeContext.TimeProvider`); `SLOWDOWN` paces every control reply the responder writes (the greeting is written by `FtpProtocolServer` and is not paced); `SLOWDOWNDATA` paces data-connection sends. 10 ms per byte via `FtpUpstreamTestCaseServerCommands.SlowByteInterval`.
- Local win-x64 run: 190, 250, 251, 1086, 1112 pass, no regressions; added to the passing list and the pass-rate row (Documentation/Conformance, a shared doc touched beyond `touches`, as the pass-rate statement test requires).
- Fast tests: Ftp 540, Conformance 951 green.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. FTP test-case half paces DELAY, SLOWDOWN and SLOWDOWNDATA on its TimeProvider; upstream tests 190, 250, 251, 1086, 1112 pass
