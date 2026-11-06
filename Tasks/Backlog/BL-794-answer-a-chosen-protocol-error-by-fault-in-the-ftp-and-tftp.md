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
completed:
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

- [ ] Named tests: FTP `STOR /up/a.txt` under a rule `451` on path `/up` gets exactly `451 <text>\r\n` and the
      content store is unchanged, while `STOR /b.txt` succeeds; FTP `RETR` likewise; TFTP RRQ under a rule gets
      the `ERROR` packet with the rule's code and message; each publishes one `FaultAppliedEvent`.
- [ ] A named test shows an FTP rule scoped to account `alice` fires only for `alice`.
- [ ] With no rule, every existing test of both libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-05: Created.
