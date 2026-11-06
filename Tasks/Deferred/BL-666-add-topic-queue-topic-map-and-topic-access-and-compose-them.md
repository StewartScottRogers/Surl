---
id: BL-666
title: Add --topic, --queue, --topic-map and --topic-access and compose them into the message bus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-645, BL-646, BL-664, BL-665]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-666 — Add --topic, --queue, --topic-map and --topic-access and compose them into the message bus

## Goal

`surl --topic <filter>[;retain;qos=N]`, `--queue <name>[;durable]`, `--topic-map <proto>:<name>=<proto>:<name>`
and `--topic-access <name>=<user|@group>:<pub|sub>,...` configure the one message bus every broker in the process
shares, with each widening option warned at start.

## Context

- Decisions: BL-641's ADR (names, grammar, refusals, bounds, help category, which option widens access) and
  BL-642's ADR decisions 3, 4 and 5.
- Builds on: BL-664 (configuration in the bus), BL-665 (MQTT passes its caller), BL-646 (group expansion), BL-645
  (item reader). The extended brokers get the same bus when their "Serve … from the surl executable" tasks
  register them; nothing here waits on them.
- Where: option rows and parsing in `Application/Surl.Cli.UnitLibrary`; composition in
  `Application/Surl.Composition.UnitLibrary`, where BL-635 composes the bus.
- A `--topic-map` naming a protocol no registered server speaks is accepted or refused as the ADR says (the
  extended brokers may be off under `SURL_EXTENSIONS=0`, ADR-0083 decision 6).
- `--aihelp` completeness (root `CLAUDE.md`).

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Cli.UnitTests` parse each option and refuse each malformed, duplicate or
      conflicting item with the ADR's exit code and text.
- [ ] Named tests in `Application/Surl.Composition.UnitTests` show topics, queues, maps and access (with an
      `@group`) reaching the composed bus, and each widening option's warning written.
- [ ] Each option passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Deferred. Split into BL-990, BL-991 and BL-992 (over the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06)
