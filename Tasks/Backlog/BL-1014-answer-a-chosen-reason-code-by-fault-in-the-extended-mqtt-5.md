---
id: BL-1014
title: Answer a chosen reason code by fault in the extended MQTT 5 broker
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785, BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: FR-062
created: 2026-11-06
completed:
---
# BL-1014 — Answer a chosen reason code by fault in the extended MQTT 5 broker

## Goal

An `error` fault rule naming `mqtt5`, `mqtt5s` or `mqtt5+ws` (or one of their listeners) makes the MQTT 5 broker
answer a matching `SUBSCRIBE` or `PUBLISH` with the chosen reason code (128 to 255) - in the `SUBACK` or
`PUBACK`, or, for a QoS 0 `PUBLISH`, a `DISCONNECT` - publishing nothing, and publish one `FaultAppliedEvent`.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md)
  decisions 4 and 9 and its extended reply table, as decision 4 says for `mqtt`; a code the packet may not carry
  becomes Unspecified error (`0x80`). `command` is the packet type, `path` the topic or filter. Consult
  `ExchangeContext.Faults` per packet after `CONNECT`; publish `FaultAppliedEvent` (BL-823's contracts and
  doubles). A rule whose reply is not a reason code, or a `delay` rule at the command stage, is noted and not
  applied. Judged by MQTT 5.0 (ADR-0083); unit tests replay bytes written from it.
- Split from BL-834 (timed out after 120 min, 2026-11-06), whose lane had this broker green. Reuse from the
  stash whose message is `darkfactory BL-834 20261106-033059` (commit `0980dd737c0c07b309c7c1f49872a4a4719d51d5`,
  called `S`; find its index with `git stash list`):
  - untracked, with `git checkout "S^3" -- <path>`:
    `Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary/Mqtt5BrokerConnection.Faults.cs`,
    `.../Surl.Extended.Protocol.Mqtt5.UnitLibrary/FaultListenUrl.cs`,
    `.../Surl.Extended.Protocol.Mqtt5.UnitTests/Mqtt5BrokerTests.Faults.cs`,
    `.../Surl.Extended.Protocol.Mqtt5.UnitTests/FaultListenUrlTests.cs`.
  - tracked, with `git diff "S^" "S" -- Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary | git apply`
    (`Mqtt5BrokerConnection.cs`; unchanged on the shared branch since the stash's base - if `git apply` refuses,
    re-make it by hand).
- `Surl.Cli` still refuses these schemes' `reply` until BL-1015; the tests here build the fault plan directly.
- Stand-in: the shared message bus is unchanged; the new observable is `FaultAppliedEvent` on the instance's
  event stream, and tests inject the fault through the fault plan double.

## Acceptance criteria

- [ ] `Mqtt5BrokerTests.Faults` exists and passes, including
      `ServeAsync_SubscribeToABAndCDUnderARuleOnAB_AnswersTheChosenCodeForABAndGrantsCD`,
      `ServeAsync_PublishUnderARule_AcknowledgesWithTheChosenCodeAndPublishesNothing`,
      `ServeAsync_QualityOfServiceZeroPublishUnderARule_DisconnectsWithTheChosenCode` and
      `ServeAsync_PublishOutsideTheRulesTopic_IsPublished`.
- [ ] Each applied rule publishes exactly one `FaultAppliedEvent`; `ServeAsync_ARuleWhoseReplyIsNotAReasonCode_IsNotedAndNotApplied` passes.
- [ ] With no rule, every existing `Surl.Extended.Protocol.Mqtt5.UnitTests` test passes unchanged.
- [ ] `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests -warnaserror` is clean, the fast
      tests are green, and the library keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 3): restored BL-834's MQTT 5 work from stash commit `0980dd73` - the four untracked files
  via `git show "S^3:<path>"` and the `Mqtt5BrokerConnection.cs` diff, which applied cleanly (the project is
  unchanged since the stash's base). The tests already carry every name the acceptance criteria give. Not built:
  `dotnet` cannot resolve the pinned SDK 10.0.401 (installed: 10.0.100-rc.1, 11.0.100-rc.2, 11.0.100), so the
  task waits on BL-1035. The restored files are left uncommitted for the shift to stash; next run, re-apply them
  the same way, then build and test.

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-834 (timed out after 120 min); reuses its MQTT 5 work from stash `darkfactory BL-834 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds; BL-834's MQTT 5 fault work is restored and needs only build and test
