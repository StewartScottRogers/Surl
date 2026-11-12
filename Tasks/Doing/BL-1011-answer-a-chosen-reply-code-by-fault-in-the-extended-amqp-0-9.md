---
id: BL-1011
title: Answer a chosen reply code by fault in the extended AMQP 0-9-1 broker
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785, BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests]
requirement: FR-062
created: 2026-11-06
completed:
---
# BL-1011 — Answer a chosen reply code by fault in the extended AMQP 0-9-1 broker

## Goal

An `error` fault rule naming `amqp` or `amqps` (or one of their listeners) makes the AMQP 0-9-1 broker answer a
matching method with the chosen reply code and text - `channel.close` or `connection.close` as the code's class
says - instead of serving it, and publish one `FaultAppliedEvent`.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md)
  decisions 4 and 9 and its extended reply table: a reply code from the AMQP 0-9-1 specification's exception
  list and its text; `command` is the method, `path` the exchange or queue (the queue for a publish to the
  default exchange). Consult `ExchangeContext.Faults` per method after login; publish `FaultAppliedEvent`
  (BL-823's contracts and doubles). A rule whose reply is not such a code, or a `delay` or `drop` rule at the
  command stage, is noted and not applied. Judged by the AMQP 0-9-1 specification (ADR-0083); unit tests replay
  bytes written from it.
- Split from BL-834 (timed out after 120 min, 2026-11-06), whose lane had this broker green. Reuse from the
  stash whose message is `darkfactory BL-834 20261106-033059` (commit `0980dd737c0c07b309c7c1f49872a4a4719d51d5`,
  called `S`; find its index with `git stash list`):
  - untracked, with `git checkout "S^3" -- <path>`:
    `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary/Amqp0BrokerConnection.Faults.cs`,
    `.../Surl.Extended.Protocol.Amqp0.UnitLibrary/FaultListenUrl.cs`,
    `.../Surl.Extended.Protocol.Amqp0.UnitTests/Amqp0BrokerConnectionTests.Faults.cs`,
    `.../Surl.Extended.Protocol.Amqp0.UnitTests/FaultListenUrlTests.cs`.
  - tracked: the five-line hook at the top of `AnswerChannelFrameAsync` in
    `Amqp0BrokerConnection.Channels.cs` (`git diff "S^" "S" -- Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary`).
    Re-make it by hand: since the stash, the shared branch added transactions, publisher confirms, headers
    exchanges and persisted exchanges to this library, so check `Amqp0BrokerConnection.Faults.cs` against the
    methods it now serves (`tx.*`, `confirm.select`) and add a test for any that names a path.
- `Surl.Cli` still refuses these schemes' `reply` until BL-1015; the tests here build the fault plan directly.
- Stand-in: the shared message bus the broker already uses is unchanged; the new observable is
  `FaultAppliedEvent` on the instance's event stream, and tests inject the fault through the fault plan double.

## Acceptance criteria

- [ ] `Amqp0BrokerConnectionTests.Faults` exists and passes, including
      `QueueDeclare_UnderARuleOnItsQueue_ClosesTheChannelWithTheChosenExceptionAndAnotherQueueIsServed`,
      `Publish_ToTheDefaultExchangeUnderARuleOnItsQueue_ClosesTheChannelAndDiscardsTheContent` and
      `Consume_UnderARuleWithAConnectionException_ClosesTheConnection`.
- [ ] Each applied rule publishes exactly one `FaultAppliedEvent`; `ExchangeDeclare_UnderARuleWhoseReplyIsNotAnException_IsNotedAndServed` passes.
- [ ] With no rule, every existing `Surl.Extended.Protocol.Amqp0.UnitTests` test passes unchanged.
- [ ] `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests -warnaserror` is clean, the fast
      tests are green, and the library keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 4): restored the four stashed files from `S^3` and re-made the hook at the top of
  `AnswerChannelFrameAsync`; this work is left uncommitted for the shift to stash. Checked against the
  methods the broker now serves: `tx.select`, `tx.commit`, `tx.rollback` and `confirm.select` name no
  exchange or queue, so they stay outside `FaultCommands` and need no path test. Dropped the doc
  comments' "ADR-0142" reference: ADR-0142 is about media types, not faults.
- Could not build: `dotnet` finds no SDK matching `global.json`'s 10.0.401 (installed: 10.0.100-rc.1,
  11.0.100-rc.2, 11.0.100). Depends on BL-1035 (Stewart reinstalls the SDK), as BL-1006 already does.

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-834 (timed out after 120 min); reuses its AMQP 0-9-1 work from stash `darkfactory BL-834 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-11: Backlog -> Doing.
