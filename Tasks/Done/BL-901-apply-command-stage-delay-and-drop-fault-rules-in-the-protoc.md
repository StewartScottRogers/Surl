---
id: BL-901
title: Apply command-stage delay and drop fault rules in the protocol servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-796]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests, Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests, Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary, Protocols/Mail/Surl.Protocol.Pop3.UnitTests, Documentation/Planning/Decisions/ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-901 — Apply command-stage delay and drop fault rules in the protocol servers

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

- [x] Named tests per mail server: a `delay;stage=command;command=RETR;ms=500` (POP3), `...command=SELECT`
      (IMAP), `...command=RCPT` (SMTP) rule holds the answer until a `ManualTimeProvider` advances 500 ms, and
      the idle timeout does not fire during it; each publishes one `FaultAppliedEvent` with kind `Delay`.
- [x] Named tests per mail server: a `drop;stage=command` rule aborts the connection with no answer to the
      matched command and publishes one `FaultAppliedEvent` with kind `Drop`.
- [x] `ReportedListenUrl` exists once, in `Surl.Protocol.Abstractions`, and the three mail libraries use it.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100%
      line and branch coverage.

## Notes

Filed by BL-796 (2026-11-05, dark factory lane 5).

Done 2026-11-06 (dark factory lane 9). Decisions in ADR-0141 (decided by Claude under Stewart's delegation):

- The drop seam already existed: `IConnection.Abort()`. Only the idle clock was missing: `IIdleClock` on
  `ExchangeContext.IdleClock` (default `NoIdleClock`), implemented explicitly by the engine's `ExchangeDeadlines`.
- One shared helper, `CommandStageFaults.FindAndApplyAsync`, holds or drops and publishes; servers keep answering
  `Error` in their own shape. Each mail server's `FindChosenErrorAsync` became `AnswerUnlessFaultedAsync`, asked at
  exactly the points BL-796 chose (IMAP `APPEND` before its `+`).
- The hold starts its `Task.Delay` before pausing the idle clock, so a test that waits for the pause
  (`RecordingIdleClock.FirstPause`) can advance its clock without racing the timer's creation.
- `ReportedListenUrl` moved to `Surl.Protocol.Abstractions`; `ServingEngine.FormatListenUrl` and the three mail
  copies are gone, their tests merged into `ReportedListenUrlTests` there. HTTP, SSH, SMB, Dict, Gopher and Telnet
  still hold their own copies; their family tasks touch those libraries.
- ADR number 0140 (0137 is unused on every branch seen; left alone in case a running lane holds it).
- `touches` grew by the ADR file and the decisions index, which no task in Doing names.
- Family follow-ups already existed (BL-902, BL-907, BL-909, BL-914); BL-902/907/909 now depend on BL-901 too.
  Filed BL-920 for MQTT and LDAP, which had none.
- Coverage: Abstractions, Core, SMTP, POP3, IMAP all 100% line and branch. IMAP's
  `ImapSession.AnswerNextCommandAsync` reports cyclomatic complexity 16 in `Measure-CodeQuality.ps1`; this task
  did not change that method (the upstream-test-case branches of BL-878 did).
- Tests: `Pop3FaultTests.ServeAsync_RetrDelayRule_...`/`ServeAsync_RetrDropRule_...`,
  `SmtpFaultTests.ServeAsync_RcptDelayRule_...`/`ServeAsync_RcptDropRule_...`,
  `ImapFaultTests.ServeAsync_SelectDelayRule_...`/`ServeAsync_SelectDropRule_...`/`ServeAsync_AppendDropRule_...`,
  `CommandStageFaultsTests`, `ServingEngineTests.ServeAsync_ServerPausesItsContextsIdleClock_...`.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. SMTP, IMAP and POP3 apply command-stage delay (idle clock paused) and drop fault rules through CommandStageFaults; ReportedListenUrl shared in Abstractions
