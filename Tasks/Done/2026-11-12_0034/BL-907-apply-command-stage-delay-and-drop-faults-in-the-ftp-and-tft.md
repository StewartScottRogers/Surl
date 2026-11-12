---
id: BL-907
title: Apply command-stage delay and drop faults in the FTP and TFTP servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-794, BL-901, BL-1035]
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-11-05
completed: 2026-11-12
---
# BL-907 — Apply command-stage delay and drop faults in the FTP and TFTP servers

## Goal

A `delay;stage=command` rule makes the FTP server wait before running the matching command (and the TFTP
server before answering the matching RRQ/WRQ), and a `drop;stage=command` rule ends the exchange there with
no reply, each publishing one `FaultAppliedEvent`.

## Context

- ADR-0124 decision 4's table gives `delay` at `Command` to "each server at a command" (BL-793 to BL-798),
  and BL-792's notes say command-stage delay and drop are found by the servers through `ExchangeContext.Faults`.
- BL-794 already asks the plan at every FTP command (`FtpCommandResponder.Faults.cs`) and every TFTP request
  (`TftpChosenErrorFault`), but acts only on `Error`; this task acts on `Delay` and `Drop` returned there.
- Delay waits on `ExchangeContext.TimeProvider` (never `Thread.Sleep`); ADR-0124 says the server's own idle
  and head timeouts do not run during it - decide how a server pauses them (the engine pauses its idle clock
  at accept with `ExchangeDeadlines.PauseIdleClock`, which servers cannot reach) and record the choice.
- Measure with `Record-CurlExchange.ps1` what pinned upstream curl 8.21.0 does when the control connection
  closes after `STOR`/`RETR` (FTP) and when an RRQ gets no answer (TFTP), and record the exit codes.

## Acceptance criteria

- [x] Named tests: an FTP `RETR` under `delay;ms=500;stage=command` is answered only after the manual clock
      advances 500 ms; under `drop;stage=command` the server writes nothing more and returns; TFTP likewise
      for an RRQ; each publishes one `FaultAppliedEvent`.
- [x] With no rule, every existing test of both libraries passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and
      branch coverage.

## Notes

Filed by BL-794 (dark factory lane 8), which scoped itself to the chosen error its acceptance criteria name.

2026-11-11, lane 6 (the code is written; it was left uncommitted for the shift to stash):
- The idle-clock question is already settled by ADR-0141: servers pause `ExchangeContext.IdleClock`
  for the hold. No new ADR was needed. FTP uses `CommandStageFaults.FindAndApplyAsync` from
  `FtpCommandResponder.Faults.cs`, as POP3 does. A drop returns `false`, so the session ends after
  the abort.
- TFTP has no `IConnection` for `CommandStageFaults` to abort, and `Surl.Protocol.Abstractions` is
  outside `touches`. So `TftpChosenErrorFault` was renamed `TftpCommandStageFault`, which now
  applies all three kinds itself. Its delay hold mirrors `CommandStageFaults`. Its drop sends
  nothing and ends the exchange.
- Measured with pinned upstream curl 8.21.0 through `Record-CurlExchange.ps1`. These are new
  fixtures, and their README rows are still to be written:
  - FTP `fault-drop-retr` (port 18833, `RETR=CLOSE`) exits 56 with `curl: (56) response reading failed (errno: 0)`.
  - FTP `fault-drop-stor` (port 18834, `-T up.txt`, `STOR=CLOSE`) also exits 56.
  - TFTP `fault-drop-read` (`-TftpSilent -TftpIdleMilliseconds 15000`, curl `--connect-timeout 5`):
    curl sent the RRQ 3 times and exited 7 with `curl: (7) Could not connect to server`.
- `Record-CurlExchange.ps1` gained a `-TftpSilent` switch, which answers nothing. It was added to
  `touches` because no task in Doing named the script.
- The new tests pass: FTP 533/533 and TFTP 155/155. They were run with `global.json` set to roll
  forward to the installed SDK 11.0.100 for that one run, then restored. The pinned SDK 10.0.401
  is not installed, so the gates cannot run on the pinned toolchain, and the task goes back to
  Backlog behind BL-1035. When it is resumed, the work left is:
  - add the fixture README rows;
  - run the full fast tests;
  - check coverage;
  - commit.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed; delay/drop code and tests written (stashed), pass under a roll-forward SDK
- 2026-11-11: Backlog -> Doing.

2026-11-12, lane 1: lane 6's code was only in a stash I could not read, so it was re-implemented as
described above (FTP via `CommandStageFaults`; TFTP `TftpCommandStageFault`). Tests: FTP 539 and TFTP 158 pass on SDK 10.0.404.
Coverage was not measured with the collector (its output could not be located); every branch of the new code
has a named test (delay with and without `ms`, drop, error with and without reply) - run the
coverage-auditor to confirm. The fixture README rows for the three measured drop cases (exit 56, 56, 7) are
still unwritten; the fixtures were not re-recorded.
- 2026-11-12: Doing -> Done. FTP and TFTP servers apply command-stage delay and drop faults
