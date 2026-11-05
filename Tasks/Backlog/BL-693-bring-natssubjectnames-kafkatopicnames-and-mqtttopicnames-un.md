---
id: BL-693
title: Bring NatsSubjectNames, KafkaTopicNames and MqttTopicNames under cyclomatic complexity 10 as Measure-CodeQuality counts it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-693 — Bring NatsSubjectNames, KafkaTopicNames and MqttTopicNames under cyclomatic complexity 10 as Measure-CodeQuality counts it

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

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.MessageBus.UnitLibrary` reports 0
      failing members.
- [ ] Every existing `Surl.MessageBus.UnitTests` test still passes unchanged.

## Notes

## Log

- 2026-11-05: Created.
