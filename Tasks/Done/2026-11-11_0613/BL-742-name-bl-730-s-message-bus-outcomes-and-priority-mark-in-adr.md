---
id: BL-742
title: Name BL-730's message bus outcomes and priority mark in ADR-0112 and ADR-0100
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-730]
touches: [Documentation/Planning/Decisions/ADR-0112-how-the-amqp-0-9-1-broker-answers-amqp-and-amqps.md, Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-742 — Name BL-730's message bus outcomes and priority mark in ADR-0112 and ADR-0100

## Goal

ADR-0112's decision 7 and ADR-0100's decisions 3.3, 3.6 and 3.7 name the `MessageBusOutcome` members,
`BusMessage.HighPriority` and the `queues/index` marks byte that BL-730 added to `Surl.MessageBus`.

## Context

- BL-730 added `QueueInUse`, `QueueNotEmpty`, `ExclusiveConsumeRefused` and `NoReadyConsumer` to
  `MessageBusOutcome`. It also added `BusMessage.HighPriority` and changed `queues/index`'s
  per-message byte from a redelivered byte (0 or 1) to a marks byte (`0x01` redelivered, `0x02`
  high priority, no other bit). See `Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md`,
  the BL-730 section, and `MessageBusFiles`' remarks.
- ADR-0112 already gives each answer in prose (decision 5's `queue.delete`, decision 6's
  `immediate` and `basic.consume`), but its decision 7 table does not name the outcomes. ADR-0112
  asks for that table to list every new outcome's AMQP 0-9-1 answer.
- BL-730 could not edit the ADRs because BL-524, then in Doing, touched
  `Documentation/Planning/Decisions`.

## Acceptance criteria

- [x] ADR-0112 decision 7 maps `QueueInUse` and `QueueNotEmpty` to `406`, and
      `ExclusiveConsumeRefused` to `403`. It also maps `NoReadyConsumer` to the `313`
      `basic.return` of decision 6.
- [x] ADR-0100 decision 3.3 states the two priority levels and the consumer stop. Its decision 3.6
      lists the four outcomes, and its decision 3.7 gives the `queues/index` marks byte as
      `MessageBusFiles` writes it.

## Notes

- Filed by BL-730.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0112 decision 7 maps QueueInUse and QueueNotEmpty to 406, ExclusiveConsumeRefused to 403 and NoReadyConsumer to the 313 basic.return; ADR-0100 3.3 states the priority levels and consumer stop, 3.6 lists the four outcomes per server, 3.7 already gave the marks byte
