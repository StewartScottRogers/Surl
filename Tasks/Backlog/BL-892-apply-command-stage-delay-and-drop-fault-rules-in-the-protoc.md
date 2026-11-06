---
id: BL-892
title: Apply command-stage delay and drop fault rules in the protocol servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-796]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests, Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests, Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary, Protocols/Mail/Surl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-892 — Apply command-stage delay and drop fault rules in the protocol servers

## Goal

A fault rule `delay;stage=command;ms=<n>` or `drop;stage=command` that matches a mail command makes the SMTP, IMAP
or POP3 server wait `ms` on the injected `TimeProvider` before answering it, or abort the connection in place of
the answer, so a test can make one command slow or cut off on purpose.

## Context

- ADR-0124 decision 4's table says command-stage `delay` is applied by "each server at a command" and names
  BL-793 to BL-798, but those tasks (BL-796 included) cover only the `error` kind; BL-792 notes "Command-stage
  delay and drop are found by the servers through `ExchangeContext.Faults` (BL-793 to BL-798)". Nothing builds
  them yet. Today the mail servers ask the plan at each command (BL-796, `*Session.Faults.cs`) and ignore any
  kind but `Error`.
- Two seams are missing from `Surl.Protocol.Abstractions`: a way for a server to stop the engine's idle clock
  during a command-stage delay (ADR-0124: "the server's own idle and head timeouts do not run during it";
  the engine has `ExchangeDeadlines.PauseIdleClock` but no server can reach it), and a way to abort the
  connection (`IConnection` has no abort). Decide them in this task, recorded in an ADR, or split the seam out.
- `ReportedListenUrl` is now copied into the three mail libraries (BL-796) because the engine's
  `ServingEngine.FormatListenUrl` is internal to `Surl.Core`; moving it to `ListenUrl` in
  `Surl.Protocol.Abstractions` lets every server share one copy. Do that here.
- The same work is due in the other server families; file one task per family once the seam lands.
- Stand-in: no store change; each application publishes `FaultAppliedEvent`.

## Acceptance criteria

- [ ] Named tests per mail server: a `delay;stage=command;command=RETR;ms=500` (POP3), `...command=SELECT`
      (IMAP), `...command=RCPT` (SMTP) rule holds the answer until a `ManualTimeProvider` advances 500 ms, and
      the idle timeout does not fire during it; each publishes one `FaultAppliedEvent` with kind `Delay`.
- [ ] Named tests per mail server: a `drop;stage=command` rule aborts the connection with no answer to the
      matched command and publishes one `FaultAppliedEvent` with kind `Drop`.
- [ ] `ReportedListenUrl` exists once, in `Surl.Protocol.Abstractions`, and the three mail libraries use it.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100%
      line and branch coverage.

## Notes

Filed by BL-796 (2026-11-05, dark factory lane 5).

## Log

- 2026-11-05: Created.
