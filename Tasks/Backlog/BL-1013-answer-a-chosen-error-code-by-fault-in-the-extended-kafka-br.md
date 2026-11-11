---
id: BL-1013
title: Answer a chosen error code by fault in the extended Kafka broker
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785, BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests]
requirement: FR-062
created: 2026-11-06
completed:
---
# BL-1013 — Answer a chosen error code by fault in the extended Kafka broker

## Goal

An `error` fault rule naming `kafka` or `kafkas` (or one of their listeners) makes the Kafka broker answer a
matching `Produce` or `Fetch` partition with the chosen error code (1 to 127) in the request's own response -
appending nothing, and for `Fetch` without waiting - instead of serving it, and publish one `FaultAppliedEvent`.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md)
  decisions 4 and 9 and its extended reply table: `command` is the API key name, `path` the topic. Consult
  `ExchangeContext.Faults` per topic after login; publish `FaultAppliedEvent` (BL-823's contracts and doubles).
  A request refused as a whole asks the plan nothing; a rule whose reply is not an error code, or a `delay` rule at
  the command stage, is noted and not applied. Judged by the Kafka protocol guide (ADR-0083); unit tests replay
  bytes written from it.
- Split from BL-834 (timed out after 120 min, 2026-11-06), whose lane had this broker green. Reuse from the
  stash whose message is `darkfactory BL-834 20261106-033059` (commit `0980dd737c0c07b309c7c1f49872a4a4719d51d5`,
  called `S`; find its index with `git stash list`):
  - untracked, with `git checkout "S^3" -- <path>`:
    `Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary/KafkaChosenErrorFault.cs`,
    `.../Surl.Extended.Protocol.Kafka.UnitLibrary/FaultListenUrl.cs`,
    `.../Surl.Extended.Protocol.Kafka.UnitTests/KafkaChosenErrorFaultTests.cs`,
    `.../Surl.Extended.Protocol.Kafka.UnitTests/FaultListenUrlTests.cs`.
  - tracked: `KafkaProduceAnswerer.cs` (`AnswerPartition` choosing request error, chosen error or append) and
    `KafkaFetchAnswerer.cs`, from `git diff "S^" "S" -- Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary`.
    Re-make them by hand: since the stash, the shared branch added `CreateTopics` and other APIs to this library
    (`KafkaApiKey`, `KafkaErrorCode`, `KafkaRequest`, `KafkaBrokerConnection` changed), so build against the
    current files rather than applying the patch.
- `Surl.Cli` still refuses these schemes' `reply` until BL-1015; the tests here build the fault plan directly.
- Stand-in: the shared message bus is unchanged; the new observable is `FaultAppliedEvent` on the instance's
  event stream, and tests inject the fault through the fault plan double.

## Acceptance criteria

- [ ] `KafkaChosenErrorFaultTests` exists and passes, including
      `ServeAsync_ProduceToARulesTopic_AnswersTheChosenErrorAndAppendsNothing`,
      `ServeAsync_ProduceToAnotherTopic_IsAppended` and `ServeAsync_FetchFromARulesTopic_AnswersTheChosenErrorWithoutWaiting`.
- [ ] Each applied rule publishes exactly one `FaultAppliedEvent`; `ServeAsync_ARuleWhoseReplyIsNotAnErrorCode_IsNotedAndNotApplied` passes.
- [ ] With no rule, every existing `Surl.Extended.Protocol.Kafka.UnitTests` test passes unchanged.
- [ ] `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests -warnaserror` is clean, the fast
      tests are green, and the library keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 5): code re-made from the BL-834 stash but not built - `dotnet build` fails with "A compatible
  .NET SDK was not found" (global.json pins 10.0.401; only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed).
  Parked behind BL-1035. The shift stashes the uncommitted work; what it holds, for the next run:
  - `KafkaChosenErrorFault.cs` and `KafkaChosenErrorFaultTests.cs` taken from `S^3`, with the stash's
    `FaultListenUrl` dropped in favour of `ReportedListenUrl.Format` (Surl.Protocol.Abstractions, which
    `KafkaCreateTopicsAnswerer` already uses), so `FaultListenUrl.cs`/`FaultListenUrlTests.cs` are not needed.
  - The stash's doc comment cited ADR-0142, which is the media-type ADR; changed to ADR-0124 decisions 4 and 9.
  - `KafkaProduceAnswerer` (now async, `AnswerPartition`: request error, chosen error, append) and
    `KafkaFetchAnswerer` (`FetchRead` takes the chosen codes; a failed partition makes `IsShort` false, so no wait)
    re-made by hand against the current files.
  - Decision: the fault is asked directly through `context.Faults.FindAsync`, not `CommandStageFaults`, because the
    task says a `delay` rule at the command stage is noted and not applied for Produce/Fetch.

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-834 (timed out after 120 min); reuses its Kafka work from stash `darkfactory BL-834 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; code re-made and left for the shift to stash
