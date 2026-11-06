---
id: BL-869
title: Name the exchange that made each message bus change in its event
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-790]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-869 — Name the exchange that made each message bus change in its event

## Goal

Every `StoreChangedEvent` and `FaultAppliedEvent` the message bus publishes for a change a server
made carries that server's exchange (`ExchangeId`), and an out-of-space `FaultPoint` carries the
exchange, listen URL and scheme, so a test can tie a bus change to the MQTT, AMQP or Kafka exchange
that made it and scope an out-of-space rule by `scheme` or `listener`.

## Context

- BL-790 publishes the bus's changes with `ExchangeId` null and asks the fault plan with only
  `Account` and `Path`, because no `MessageBus` call is told which exchange is calling.
- ADR-0124 decision 1 (`StoreChangedEvent.ExchangeId`) and decision 4 (`FaultPoint`'s listen URL and
  scheme); the scheme is reachable through the event of the exchange whose ID the change names.
- Likely shape: `MessageBus.Connect` (or `BusConnection`) takes the server's `ExchangeContext`
  identity - exchange ID, listen URL, scheme - so every operation made through the connection names
  it; the parameterless forms stay anonymous with none. Wiring each broker to pass it is the brokers'
  own follow-up tasks (file them when this lands).

## Acceptance criteria

- [ ] Named `MessageBusEventTests` show a publish, send, delivery and acknowledgement made through a
      connection given an exchange ID carry that ID, and the anonymous forms still carry none.
- [ ] A named `MessageBusOutOfSpaceTests` test shows the `FaultPoint` carries the connection's
      exchange ID, listen URL and scheme.
- [ ] Every existing `Surl.MessageBus.UnitTests` test passes unchanged, the solution builds, the fast
      tests are green, and the library keeps 100% line and branch coverage.

## Notes

Filed by BL-790's run (dark factory lane 1).

## Log

- 2026-11-05: Created.
