---
id: BL-1171
title: Reset, snapshot and restore the MQTT 5 broker's sessions with the message bus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1152, BL-1158]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-14
completed:
---
# BL-1171 — Reset, snapshot and restore the MQTT 5 broker's sessions with the message bus

## Goal

A whole-instance reset, snapshot and restore also deal with the MQTT 5 broker's sessions, pending wills and queued deliveries as BL-1152's ADR decides, so a persistent session's queued message from one test is never delivered in the next.

## Context

- `Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary/Mqtt5BrokerState.cs` holds `sessions` (each `Mqtt5Session` with a bounded channel of queued deliveries, `Mqtt5Session.cs`), `pendingWills` and `connectedClients`; retained messages are already on the bus. `ComposedStores` does not know the state exists, so `MessageBus.Reset` leaves offline sessions' queued messages in place.
- Implement what BL-1152's ADR decides for each piece and register it through BL-1158's seam in `Application/Surl.Composition.UnitLibrary` (the broker is built in `ExtendedProtocolServers.cs` as `new Mqtt5Broker(new Mqtt5BrokerState(services.MessageBus), ...)`; the state must be reachable by the registration). Pending wills use the injected `TimeProvider` already.
- Judged by the MQTT 5.0 specification (ADR-0093); no upstream curl involved.

## Acceptance criteria

- [ ] Named tests in the MQTT 5 tests: a persistent session with queued deliveries, a pending will and a connected client each end up after reset as the ADR decides (at least: no queued delivery survives a reset); snapshot then restore behaves as the ADR decides.
- [ ] A named composition test: a QoS 1 message queued for an offline `clean start = 0` session is not delivered when that client reconnects after `ComposedStores.ResetAsync`.
- [ ] Every existing MQTT 5 and composition test passes unchanged; `dotnet build Surl.slnx -warnaserror` is clean and the fast tests are green.
- [ ] Both libraries keep 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-14: Created.
