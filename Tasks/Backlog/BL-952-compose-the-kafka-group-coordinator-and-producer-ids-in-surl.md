---
id: BL-952
title: Compose the Kafka group coordinator and producer ids in surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-973, BL-974, BL-949, BL-950, BL-951]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-952 — Compose the Kafka group coordinator and producer ids in surl

## Goal

`surl kafka://` and `kafkas://` serve consumer groups, committed offsets, CreateTopics and InitProducerId: one
`KafkaGroupCoordinator` and one producer-state holder per instance, shared by both listeners, with the answerers of
BL-948 to BL-951 registered, and the help and `--aihelp` text naming them and `$group/<id>` access.

## Context

- Decision: [ADR-0145](../../Documentation/Planning/Decisions/ADR-0145-how-the-kafka-broker-answers-consumer-groups-committed-offsets-createtopics-and-initproducerid.md) decisions 0 and 6. BL-522 composes the broker in
  `Application/Surl.Composition.UnitLibrary` (`ExtendedProtocolServers.Registrations`, the shared bus in
  `ComposedProtocolServices.MessageBus`); this task adds the new answerers there, the coordinator constructed with
  the instance's `TimeProvider` (never a static, ADR-0124: two `Surl.Api` servers never share one).
- `Surl.Cli`: the `--topic-access` help line and `--aihelp` facts say a group is named `$group/<id>` and needs `sub`;
  the `kafka` topic's `AiHelpProse.TopicAbout` names the APIs now served. Behaviour an `AiHelpProse` paragraph or
  `AiHelpExamples` entry describes changes with it in the same diff (root `CLAUDE.md`).

## Acceptance criteria

- [ ] A `Surl.Composition.UnitTests` test shows the `kafka` and `kafkas` brokers of one instance answering with the
      same group coordinator (a group joined on one is described on the other), and two composed instances not
      sharing one.
- [ ] A `Surl.Composition.UnitTests` test shows ApiVersions over a composed `kafka` listener listing the ranges ADR-0145
      decision 1 gives.
- [ ] `Surl.Cli.UnitTests` pin the `--topic-access` help text naming `$group/<id>`, and the `--aihelp` completeness
      tests named in root `CLAUDE.md` pass, `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`
      included.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- Filed by BL-701 (ADR-0145).

## Log

- 2026-11-06: Created.
- 2026-11-13: depends-on BL-522 replaced by BL-973 and BL-974: BL-522 was deferred on 2026-11-06 and split into those two, both done.
