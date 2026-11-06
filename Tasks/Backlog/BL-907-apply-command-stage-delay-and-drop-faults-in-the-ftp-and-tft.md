---
id: BL-907
title: Apply command-stage delay and drop faults in the FTP and TFTP servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-794]
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-11-05
completed:
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

- [ ] Named tests: an FTP `RETR` under `delay;ms=500;stage=command` is answered only after the manual clock
      advances 500 ms; under `drop;stage=command` the server writes nothing more and returns; TFTP likewise
      for an RRQ; each publishes one `FaultAppliedEvent`.
- [ ] With no rule, every existing test of both libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and
      branch coverage.

## Notes

Filed by BL-794 (dark factory lane 8), which scoped itself to the chosen error its acceptance criteria name.

## Log

- 2026-11-05: Created.
