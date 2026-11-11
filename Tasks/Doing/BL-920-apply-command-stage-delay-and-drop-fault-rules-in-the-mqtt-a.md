---
id: BL-920
title: Apply command-stage delay and drop fault rules in the MQTT and LDAP servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-901, BL-797]
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests]
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

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
