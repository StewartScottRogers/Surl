---
id: BL-1190
title: Reset, snapshot and restore the Kafka broker's consumer groups and producer states with the message bus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1152, BL-1158]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-14
completed:
---
# BL-1190 — Reset, snapshot and restore the Kafka broker's consumer groups and producer states with the message bus

## Goal

A whole-instance reset, snapshot and restore also deal with the Kafka broker's consumer groups (`KafkaGroupCoordinator`) and idempotent-producer sequence state (`KafkaProducerStates`) as ADR-0159 decides, so a producer's sequence numbers from one test are not judged against the emptied log of the next.

## Context

- ADR-0159 (BL-1152) classifies both as live session state held outside `MessageBus` and decides what reset, snapshot and restore do to each; read its Kafka section first.
- `KafkaGroupCoordinator.cs` holds `groups` (members, pending members, static instances); `KafkaProducerStates.cs` holds per `(ProducerId, Topic)` sequence windows and the next producer id. Committed offsets live on the bus already.
- Register through BL-1158's seam in `Application/Surl.Composition.UnitLibrary`; the broker is built in `ExtendedProtocolServers.cs`.
- Judged by the Kafka protocol specification (ADR-0105); no upstream curl involved.

## Acceptance criteria

- [ ] Named Kafka tests: after reset a producer's duplicate-detection window is empty and a batch with a sequence the old log would have judged a duplicate is appended; groups are dropped as ADR-0159 decides and a member's next heartbeat is answered `UNKNOWN_MEMBER_ID`; snapshot, change, restore behaves as the ADR decides; a snapshot from another broker is refused.
- [ ] A named composition test: `ComposedStores.ResetAsync` clears both after a produce and a group join over `kafka://`.
- [ ] Every existing Kafka and composition test passes unchanged; `dotnet build Surl.slnx -warnaserror` is clean and the fast tests are green.
- [ ] Both libraries keep 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
