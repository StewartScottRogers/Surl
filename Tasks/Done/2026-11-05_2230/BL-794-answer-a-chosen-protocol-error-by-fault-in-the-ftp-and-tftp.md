---
id: BL-794
title: Answer a chosen protocol error by fault in the FTP and TFTP servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-794 — Answer a chosen protocol error by fault in the FTP and TFTP servers

## Goal

When a chosen-error fault rule matches, the FTP server answers the matching command (for example `STOR` or `RETR`
on a path) with the rule's reply code and text, and the TFTP server answers the matching read or write request
with an `ERROR` packet carrying the rule's code and message, so a test can make one transfer fail on purpose.

## Context

- Decision: BL-822's ADR decision 4 (what "command" means per protocol, reply checking, whether a failed FTP
  transfer command still opens the data connection).
- Contract: BL-823 (`ExchangeContext.Faults`, `FaultAppliedEvent`). Tests use BL-823's scripted fault plan and
  recording publisher, with `InMemoryConnection`, `InMemoryDataConnections` and TFTP's `ScriptedDatagramFlow`.
- Where: `Surl.Protocol.Ftp` (consult the plan per command, after login, before the content store) and
  `Surl.Protocol.Tftp` (per RRQ/WRQ; TFTP has no login, so an account-scoped rule never matches it).
- Request bytes from pinned upstream curl 8.21.0 (`Record-CurlExchange.ps1`), never the Curl port (ADR-0003).
  Record in Notes the exit code upstream curl 8.21.0 gives for each named case.
- Stand-in: no store change; visible as a `FaultAppliedEvent` and the exchange's bytes.

## Acceptance criteria

- [x] Named tests: FTP `STOR /up/a.txt` under a rule `451` on path `/up` gets exactly `451 <text>\r\n` and the
      content store is unchanged, while `STOR /b.txt` succeeds; FTP `RETR` likewise; TFTP RRQ under a rule gets
      the `ERROR` packet with the rule's code and message; each publishes one `FaultAppliedEvent`.
- [x] A named test shows an FTP rule scoped to account `alice` fires only for `alice`.
- [x] With no rule, every existing test of both libraries passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

Delivered 2026-11-05, dark factory lane 8. Choices within ADR-0124 decision 4 (details it leaves open, not
new decisions, so no ADR):

- **FTP** (`FtpCommandResponder.Faults.cs`): every implemented command that passes the login check is asked
  about once, before it runs; an unimplemented command (502) and one refused 530 before login are not.
  `command` is the verb; `path` is the path the command names resolved against the current directory, as
  `/seg/seg` decoded (`LIST`/`NLST` without ls options; a listing without a path is the current directory;
  no path for a command that names none or one that does not resolve). `account` is the name a *checked*
  login named (`PasswordLoginVerdict.Accepted`); an unchecked anonymous login names no account. A matching
  `Error` rule's reply is sent as `<reply>\r\n` in place of running the command, so `STOR`/`RETR` open no
  data connection (the passive listener `EPSV` prepared is never accepted on) and the store is untouched.
  Target in `FaultAppliedEvent`: `<VERB> <path>`, or the bare verb.
- **TFTP** (`TftpChosenErrorFault`): asked after the request parses, before the store; `command` `RRQ`/`WRQ`,
  `path` the decoded request path, no account. Reply `<code 0-7> <text>` becomes that ERROR from the transfer
  port; a reply not starting with a code 0-7 (refused by the reader, but possible from a plan built in code)
  is sent whole as ERROR 0.
- Only `Error` is acted on; `Delay`/`Drop` at the command stage are BL-907.
- The listen URL in each point uses the engine's form; with no shared helper in Abstractions it is copied
  into each library as `FaultListenUrl` (Abstractions left alone while sibling chosen-error tasks run).
  BL-908 folds the copies into `ListenUrl`.
- `Surl.Protocol.Ftp.UnitTests` now references `Surl.Core.UnitLibrary` (tests only), so the `/up` rule is
  matched by the engine's own `FaultPlan` segment-prefix rule. Inside this task's touches.

Upstream curl 8.21.0 exit codes (`Record-CurlExchange.ps1`, fixtures `fault-stor`, `fault-retr`, `fault-read`):
FTP `STOR` answered 451 -> 25 (`Failed FTP upload: 451`); FTP `RETR` answered 451 -> 19
(`RETR response: 451`); TFTP RRQ answered ERROR 2 -> 69 (`TFTP: Access Violation`; the exit follows the
code: 1 -> 68, 2 -> 69, 3 -> 70, 0 and 4 -> 71 per the existing fixtures). curl sent `QUIT` after each FTP failure.

Tests: `FtpFaultTests`, `FaultListenUrlTests` (both libraries), `TftpChosenErrorFaultTests`.
Ftp.UnitTests 531, Tftp.UnitTests 153, solution fast tests 19,774, all green. Both libraries 100% line and
branch, worst CRAP 10. Follow-ups filed: BL-907, BL-908.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. FTP answers a matching command and TFTP a matching RRQ/WRQ with a chosen-error fault rule's reply, before the store, publishing FaultAppliedEvent
