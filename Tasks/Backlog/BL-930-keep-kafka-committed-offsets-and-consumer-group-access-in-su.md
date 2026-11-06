---
id: BL-930
title: Keep Kafka committed offsets and consumer group access in Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-929]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-930 — Keep Kafka committed offsets and consumer group access in Surl.MessageBus

## Goal

`Surl.MessageBus` keeps a bounded table of committed offsets keyed by consumer group and canonical topic - stored,
read, listed, reset, snapshotted, restored, persisted in `.surl/bus/offsets` and published as `StoreChange.Committed`
- and answers whether a caller may use a group (`$group/<id>` in the access grants), so the Kafka broker's
OffsetCommit and OffsetFetch (BL-932) and its group access checks (BL-931) have a store.

## Context

- Decision: [ADR-0145](../../Documentation/Planning/Decisions/ADR-0145-how-the-kafka-broker-answers-consumer-groups-committed-offsets-createtopics-and-initproducerid.md) decisions 0, 4, 6, 9 and 10 (amending ADR-0100 decision 3.3 and ADR-0102 decision
  3.4). The table is not a message: no other protocol reads it.
- An entry: group id, canonical topic, offset (INT64 as given), leader epoch (INT32, -1 when none), metadata
  (nullable string) and the commit time from the bus's `TimeProvider`. Never expired by time. Replacing an entry
  always succeeds; a new entry past `DefaultMaxCommittedOffsets` (10000, a constructor parameter like the bus's other
  bounds) is `StoreFull`, which the Kafka column of ADR-0100 decision 3.6 answers 44.
- A commit is checked against the access grants (subscribe on the topic and on `$group/<id>`, else `AccessDenied`)
  and an `out-of-space` rule at the `StoreWrite` point with the change name as path, like a publish, and publishes
  one `StoreChangedEvent(Bus, Committed, "$group/<group, % and / percent-encoded>/<canonical topic>", null, null)`.
- Group access: `TopicAccessPolicy` gains a group check beside `MayUseQueue`: `sub` on `$group/<id>`, the id one level
  whatever it holds, the `$` rule applying; with no grants every caller may use every group (ADR-0145 decision 6).
- Listing: the groups that hold any entry (for ListGroups), and one group's entries in ordinal order of topic.
- Persistence: `.surl/bus/offsets`, header `SURL-BUS-OFFSETS-1` and a line feed, entry count, per entry in ordinal
  order of group then topic: group, topic, offset (8), leader epoch (4), flags (`0x01` metadata follows), metadata,
  commit time (8, UTC ticks); big-endian, strings a 4-byte length and UTF-8; rewritten whole through a temporary name
  by `SaveChangesAsync`, read once by `LoadAsync`, a malformed or over-bound file `not a message bus file`.
  `MessageBusFiles`' remarks and ADR-0100 decision 3.7's file list are the format's documents; update the remarks.
- `Reset` empties the table, `TakeSnapshot` copies it within `MaxSnapshotBytes`, `Restore` returns it (ADR-0124
  decision 3); a bus on disk refuses all three as today. No seed method (ADR-0145 decision 4).

## Acceptance criteria

- [ ] Named `Surl.MessageBus.UnitTests` tests show: a commit stored and read back with its offset, leader epoch, null
      and non-null metadata and commit time; a replacement; the 10001st new entry refused `StoreFull` while
      replacing an existing one succeeds; `AccessDenied` without subscribe access to the topic and without `sub` on
      `$group/<id>`; an out-of-space rule refusing a commit; one `Committed` event per commit with the encoded name.
- [ ] A `TopicAccessPolicyTests` test shows `$group/<id>` granted by a literal or `+` grant, never by `#`, and every
      group open with no grants.
- [ ] `MessageBusFilesTests` pin the `offsets` file's bytes for two groups, and `MessageBusPersistenceTests` show
      entries surviving a save and load, and a malformed file refused with `not a message bus file`.
- [ ] `MessageBusSeedResetSnapshotTests` show `Reset` emptying the table and `Restore` returning a snapshot's entries.
- [ ] `Kafka.md`'s `Kafka:Surl-Offsets-1` and `Kafka:Surl-Offsets-2` rows name these tests, and
      `Kafka:Surl-GroupAccess-1` names the access test beside BL-931 and BL-932.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-701 (ADR-0145).

## Log

- 2026-11-06: Created.
