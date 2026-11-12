---
id: BL-991
title: Compose configured topics, queues, topic maps and topic access into the message bus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-990, BL-664, BL-665]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-991 — Compose configured topics, queues, topic maps and topic access into the message bus

## Goal

The topics, queues, topic maps and topic access in `SurlCommandLine` (from BL-990) configure the one message bus
every broker in the process shares, with each widening setting warned at start.

## Context

- Second of the three tasks BL-666 was split into (see BL-990).
- Decisions: ADR-0101 (which setting widens access, warned per ADR-0032 section 9) and ADR-0102 decisions 3, 4
  and 5.
- Builds on: BL-664 (configuration in the bus), BL-665 (MQTT passes its caller), BL-646 (group expansion). The
  extended brokers get the same bus when their "Serve … from the surl executable" tasks register them; nothing here
  waits on them.
- Where: `Application/Surl.Composition.UnitLibrary`, where BL-635 composes the bus.
- Stand-in: the message bus is the shared store; its seeding and events are BL-805's and ADR-0127's.

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Composition.UnitTests` show topics, queues, maps and access (with an
      `@group`) reaching the composed bus.
- [ ] Named tests show each widening setting's warning written at start.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
