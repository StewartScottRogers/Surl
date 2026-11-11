---
id: BL-920
title: Apply command-stage delay and drop fault rules in the MQTT and LDAP servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-901, BL-797, BL-1035]
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests, Documentation/Planning/Decisions/ADR-0139-how-the-mqtt-and-ldap-servers-answer-a-chosen-error.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-920 — Apply command-stage delay and drop fault rules in the MQTT and LDAP servers

## Goal

A fault rule `delay;stage=command;ms=<n>` or `drop;stage=command` that matches an MQTT packet or LDAP operation
makes the server hold its answer `ms` on the injected `TimeProvider`, or abort the connection in place of it.

## Context

- ADR-0140 (BL-901) decides how: call `CommandStageFaults.FindAndApplyAsync` from `Surl.Protocol.Abstractions`
  at each point where the server already asks the plan for a chosen error (ADR-0139, BL-797), answer as usual
  after a `Delay`, end the session after a `Drop`, and answer an `Error` as today. The helper pauses
  `ExchangeContext.IdleClock`, publishes the `FaultAppliedEvent` and aborts the connection.
- ADR-0139 currently notes a `delay` or `drop` at a command and does not apply it; that sentence changes.
- Move each server's listen URL formatting, if it has its own, to `ReportedListenUrl.Format`.
- The mail servers' tests (`Pop3FaultTests`, `ImapFaultTests`, `SmtpFaultTests`) show the shape: a
  `RecordingIdleClock` and a `ManualTimeProvider`.

## Acceptance criteria

- [ ] Named tests: an MQTT `delay;stage=command;command=SUBSCRIBE;ms=500` and an LDAP `...command=search;ms=500`
      hold the answer until the clock advances 500 ms, with the idle clock paused throughout, and publish one
      `FaultAppliedEvent` with kind `Delay`.
- [ ] Named tests: a `drop;stage=command` rule at an MQTT `PUBLISH` and at an LDAP `search` aborts the connection
      with no answer and publishes one `FaultAppliedEvent` with kind `Drop`.
- [ ] ADR-0139 says what now happens to a command-stage `delay` or `drop`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line
      and branch coverage.

## Notes

Filed by BL-901 (2026-11-06, dark factory lane 9): BL-902, BL-907, BL-909 and BL-914 cover the other families;
MQTT and LDAP had none.

2026-11-11, dark factory lane 4:
- The ADR the task calls ADR-0140 is ADR-0141 ("How a server applies a command-stage delay or drop"); ADR-0140 is QUIC.
- Added ADR-0139's file to `touches`: criterion 3 edits it, and no task in Doing names it.
- Written but NOT built or tested: the pinned .NET SDK 10.0.401 is not installed on the factory machine
  (`dotnet --list-sdks` has 10.0.100-rc.1 and 11.0.100), so `dotnet build` fails at SDK resolution. Returned to
  Backlog behind BL-1035 (Stewart: reinstall the SDK). The code is left uncommitted for the shift to stash; the
  next run should apply that stash, then build, run the fast tests and measure coverage before ticking boxes.
- Design: both servers call `CommandStageFaults.FindAndApplyAsync` in their existing `FindChosen...Async`; a drop
  sets `isDroppedByAFault`, after which MQTT sends no CONNACK/SUBACK, asks no further SUBSCRIBE filter and
  publishes nothing, and LDAP sends no response and ends the session. `FaultListenUrl` copies are deleted in
  favour of `ReportedListenUrl.Format`. The old "delay is noted and not applied" tests became "out-of-space is
  noted and not applied". New tests: MqttFaultTests (SUBSCRIBE delay, PUBLISH/CONNECT/SUBSCRIBE drop) and
  LdapFaultTests (search delay, search/compare/add drop).

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: the pinned .NET SDK 10.0.401 is not installed, so nothing builds; code written and left for the shift to stash
