---
id: BL-1159
title: Publish Redis keyspace changes on the event stream and apply the out-of-space fault at a Redis write
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1157]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: FR-060
created: 2026-11-14
completed:
---
# BL-1159 — Publish Redis keyspace changes on the event stream and apply the out-of-space fault at a Redis write

## Goal

Every write and delete a Redis client makes to the keyspace publishes a `StoreChangedEvent` with the Redis store kind, and an `out-of-space` fault rule makes a Redis write answer the out-of-space error BL-1151's ADR names, as the four shared stores already do.

## Context

- Stand-in audit 2026-11-14: `Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary` publishes only `FaultAppliedEvent` for chosen errors (`RedisCommandFaults.cs`). Writes go through `RedisKeyspace`/`RedisDatabase` and the command classes (`RedisStringCommands.cs`, `RedisKeyCommands.cs`, `RedisHashCommands.cs`, `RedisListCommands.cs`, `RedisSetCommands.cs`).
- Follow the shared stores' pattern: `Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.Events.cs`, `Foundation/Surl.Content.UnitLibrary/ContentStoreEventsAndFaults.cs`, `Protocols/Messaging/Surl.MessageBus.UnitLibrary/MessageBus.Events.cs`: `StoreChangedEvent(Store, Change, Name, ByteCount, ExchangeId)` through `ExchangeContext.Events`, and `ExchangeContext.Faults.FindAsync(new FaultPoint(FaultStage.StoreWrite) { ... Path = key })` before a write, publishing `FaultAppliedEvent` when applied. Decisions: ADR-0124 decisions 1 and 4, ADR-0127 and ADR-0129 (what the mail and content stores publish and how they run out of space), and BL-1151's ADR for Redis's `Name`, `Change` values and the error reply.
- Judged by the Redis documentation (ADR-0083, ADR-0106); no upstream curl involved.

## Acceptance criteria

- [ ] Named tests: a `SET`, a `DEL` and one write per collection family (hash, list, set) each publish exactly one `StoreChangedEvent` with the Redis `StoreKind`, the `StoreChange` and `Name` BL-1151's ADR decides, and the exchange ID; a read publishes none.
- [ ] Named tests: with an `out-of-space` rule whose `path` matches the key, the write answers the RESP error the ADR names, the key is unchanged, and one `FaultAppliedEvent` (`OutOfSpace`, `StoreWrite`) is published; a rule for another key changes nothing.
- [ ] With no subscriber and no rule, every existing Redis test passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the Redis library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
