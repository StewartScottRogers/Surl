---
id: BL-1170
title: Reset, snapshot and restore the AMQP 0-9-1 broker's declared exchanges and bindings with the message bus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1152, BL-1158]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-14
completed:
---
# BL-1170 — Reset, snapshot and restore the AMQP 0-9-1 broker's declared exchanges and bindings with the message bus

## Goal

A whole-instance reset, snapshot and restore also return the AMQP 0-9-1 broker's exchanges and bindings as BL-1152's ADR decides, so an exchange one test declared is not there for the next.

## Context

- `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary/Amqp0ExchangeTable.cs` holds the built-in exchanges (`amq.direct`, `amq.fanout`, `amq.topic`, `amq.match`, the default exchange) and every declared exchange with its bindings; `Amqp0ExchangeFiles.cs` saves durable ones with `--directory`. `ComposedStores` does not know it exists.
- Add reset, take-snapshot and restore to the table per BL-1152's ADR (one lock, opaque snapshot restorable only into the table that made it) and register it through BL-1158's seam in `Application/Surl.Composition.UnitLibrary` (`ExtendedProtocolServers.cs` builds the broker; `SurlServerComposition.LoadAmqp0ExchangesAsync` loads its files).
- Live channels follow ADR-0124 decision 3's "live sessions" rule as the ADR applies it.

## Acceptance criteria

- [ ] Named tests in the AMQP 0-9-1 tests: after a client declares an exchange and binds a queue, reset leaves only the exchanges the ADR says remain; snapshot, change, restore gives back the snapshot's exchanges and bindings; a snapshot from another broker is refused.
- [ ] A named composition test: `ComposedStores.ResetAsync` removes an exchange declared over an `amqp://` exchange, and a declare after it succeeds as on a fresh broker.
- [ ] With `--directory`, the behaviour is the ADR's (refusal or otherwise), proven by a test.
- [ ] Every existing AMQP 0-9-1 and composition test passes unchanged; `dotnet build Surl.slnx -warnaserror` is clean and the fast tests are green.
- [ ] Both libraries keep 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
