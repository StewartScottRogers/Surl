---
id: BL-703
title: Amend ADR-0100 decision 3.7 with the message bus file formats BL-634 pinned
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-634]
touches: [Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-703 — Amend ADR-0100 decision 3.7 with the message bus file formats BL-634 pinned

## Goal

ADR-0100 decision 3.7 states the byte formats, file layout and save rules of `<path>/.surl/bus` that BL-634 built
into `Surl.MessageBus`, so the decision says what the code does.

## Context

- ADR-0100 decision 3.7 ends "BL-634 fixes the byte formats". BL-634's `touches` did not include
  `Documentation/Planning/Decisions` (BL-516 held it), so it recorded its choices in its own Notes (Tasks, BL-634),
  in `Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md`, in the remarks of `MessageBusFiles` and
  `BusMessageCodec`, and pinned the bytes in `MessageBusFilesTests` and `BusMessageCodecTests`. Same pattern as
  BL-694 and BL-697.
- What to state: the four headers (`SURL-BUS-RETAINED-1`, `SURL-BUS-QUEUES-1`, `SURL-BUS-MESSAGE-1`,
  `SURL-BUS-LOG-1`, each with a line feed); the message form (flags byte, QoS, UTC ticks, optional fields,
  headers, payload); `queues/messages/<16 hex digits>` and `logs/<16 hex digits>`, the log file holding its topic
  and start offset; which queues persist (durable, not exclusive, not auto-delete), unacknowledged messages
  loaded as redelivered; logs appended while they only grow and rewritten whole otherwise; a file past a bus
  bound refused as malformed; `MessageBusOutcome.StorageFailed` returned by `MessageBus.SaveChangesAsync` and
  `LoadAsync`.

## Acceptance criteria

- [ ] ADR-0100 decision 3.7 states each file's byte format as `MessageBusFiles`' remarks and `BusMessageCodec`'s
      remarks give it, marked "Decided by Claude under Stewart's delegation" (BL-634).
- [ ] ADR-0100 decision 3.7 states which queues persist, how an unacknowledged message loads, when a log file is
      appended to and when rewritten, and that a file past a bound is refused with `not a message bus file`.
- [ ] Every statement added is true of `Protocols/Messaging/Surl.MessageBus.UnitLibrary` as it is.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
