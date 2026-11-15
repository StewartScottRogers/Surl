---
id: BL-1171
title: Reset, snapshot and restore the MQTT 5 broker's sessions with the message bus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1152, BL-1158]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-061
created: 2026-11-14
completed: 2026-11-15
---
# BL-1171 — Reset, snapshot and restore the MQTT 5 broker's sessions with the message bus

## Goal

A whole-instance reset, snapshot and restore also deal with the MQTT 5 broker's sessions, pending wills and queued deliveries as BL-1152's ADR decides, so a persistent session's queued message from one test is never delivered in the next.

## Context

- `Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary/Mqtt5BrokerState.cs` holds `sessions` (each `Mqtt5Session` with a bounded channel of queued deliveries, `Mqtt5Session.cs`), `pendingWills` and `connectedClients`; retained messages are already on the bus. `ComposedStores` does not know the state exists, so `MessageBus.Reset` leaves offline sessions' queued messages in place.
- Implement what BL-1152's ADR decides for each piece and register it through BL-1158's seam in `Application/Surl.Composition.UnitLibrary` (the broker is built in `ExtendedProtocolServers.cs` as `new Mqtt5Broker(new Mqtt5BrokerState(services.MessageBus), ...)`; the state must be reachable by the registration). Pending wills use the injected `TimeProvider` already.
- Judged by the MQTT 5.0 specification (ADR-0093); no upstream curl involved.

## Acceptance criteria

- [x] Named tests in the MQTT 5 tests: a persistent session with queued deliveries, a pending will and a connected client each end up after reset as the ADR decides (at least: no queued delivery survives a reset); snapshot then restore behaves as the ADR decides.
- [x] A named composition test: a QoS 1 message queued for an offline `clean start = 0` session is not delivered when that client reconnects after `ComposedStores.ResetAsync`.
- [x] Every existing MQTT 5 and composition test passes unchanged; `dotnet build Surl.slnx -warnaserror` is clean and the fast tests are green.
- [x] Both libraries keep 100% line and branch coverage with no method over complexity 10.

## Notes

- `touches` grew by `Foundation/Surl.Protocol.Abstractions.*` (no task in Doing names it): the new `StoreKind.Sessions` member, as BL-1170 added `Exchanges`.
- Decided under ADR-0159 decision 2: snapshot keeps each offline session's full Session Expiry Interval, started again on restore (no remaining time is stored); `ConnectedClients` untouched; `Mqtt5Session` queue is now multi-reader safe (`SingleReader = false`) because reset drains a connected session's queue from another thread.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. MQTT 5 sessions, pending Wills and queued deliveries reset, snapshot and restore with the bus (ADR-0159)
