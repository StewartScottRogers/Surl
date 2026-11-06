---
id: BL-695
title: Bring NatsSubjectNames, KafkaTopicNames and MqttTopicNames under cyclomatic complexity 10 as Measure-CodeQuality counts it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-695 — Bring NatsSubjectNames, KafkaTopicNames and MqttTopicNames under cyclomatic complexity 10 as Measure-CodeQuality counts it

## Goal

`Measure-CodeQuality.ps1 -Library Surl.MessageBus.UnitLibrary` reports no failing members.

## Context

- Measured on 2026-11-05 during BL-631. Line and branch coverage are both 100%. The script still reports three
  methods over complexity 10: `NatsSubjectNames.FromCanonicalFilter(string)` at 16,
  `KafkaTopicNames.IsKafkaTopic(string, char)` at 12 and `MqttTopicNames.ToCanonicalSharedSubscription(string)`
  at 12. All three came from BL-630. The build-time CA1502 check passes them, so the two measures count
  differently.
- Fix it by splitting the methods. Never raise a threshold.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.MessageBus.UnitLibrary` reports 0
      failing members.
- [x] Every existing `Surl.MessageBus.UnitTests` test still passes unchanged.

## Notes

- 2026-11-06: The measure reported five failing members, not three: also `MessageBus.RequireWithinBounds` (12)
  and `MessageBus.ReplaceQueues` (12), in the same library, so the goal (no failing members) covered them too.
  The pipeline says `feature`, but this is a behaviour-preserving split with no design to plan, so it was made
  directly. All five were split into private methods: `NatsSubjectNames.FromCanonicalFilter` ->
  `FromCanonicalFilterLevels`, `EndsWithOneOrMoreLevels`, `JoinFilterLevels`, `WithFullWildcardAppended`;
  `KafkaTopicNames.IsKafkaTopic` -> `IsDotOrDotDot`, `HasOnlyKafkaCharacters`;
  `MqttTopicNames.ToCanonicalSharedSubscription` -> `ToCanonicalGroupAndFilter`, `IsShareName`;
  `RequireWithinBounds` -> `RequireLogsWithinBounds`, `RequireRetainedWithinBounds`, `RequireQueuesWithinBounds`;
  `ReplaceQueues` -> `EmptyQueuesAndRemoveUnusedOnes`. The script counts more than CA1502 does, apparently
  the compiler's lambda-cache and pattern branches, so lambda-heavy methods were split by stage.
  Result: 469 members, 0 failing, worst CRAP 10, 100% line and branch; all 500 tests pass unchanged.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Surl.MessageBus.UnitLibrary has no member over complexity 10 as Measure-CodeQuality counts it (five split, 100%/100%, 500 tests unchanged)
