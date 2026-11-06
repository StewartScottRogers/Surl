---
id: BL-915
title: Point Surl.MessageBus CLAUDE.md at ADR-0100's queue outcomes now BL-694 records them
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-694]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-915 — Point Surl.MessageBus CLAUDE.md at ADR-0100's queue outcomes now BL-694 records them

## Goal

`Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md` no longer says that ADR-0100 does not
name the queue outcomes, because BL-694 added them to the ADR.

## Context

- The "Bounds and outcomes" bullet of that `CLAUDE.md` ends: "The queue outcomes that ADR-0100 does
  not name yet (`InvalidQueueName`, `UnknownQueue`, `QueueLocked`, `QueueFlagsDiffer`,
  `UnknownDeliveryTag`) follow AMQP 0-9-1's 404, 405 and 406. BL-694 adds them to the ADR."
- BL-694 added them to ADR-0100 decision 3.6 ("The queue outcomes") and the named-queue rules to
  decision 3.3 ("The named queue's rules"). BL-694 could not edit the `CLAUDE.md` because BL-704
  held it.

## Acceptance criteria

- [ ] The `CLAUDE.md` bullet names ADR-0100 decision 3.6 as where the five queue outcomes and each
      server's answer are recorded, and no longer says "does not name yet" or "BL-694 adds them".

## Notes

## Log

- 2026-11-06: Created.
