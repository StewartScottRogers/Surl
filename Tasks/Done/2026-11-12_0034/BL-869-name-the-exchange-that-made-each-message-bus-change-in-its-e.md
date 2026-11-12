---
id: BL-869
title: Name the exchange that made each message bus change in its event
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-790, BL-1035]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-11
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

- [x] Named `MessageBusEventTests` show a publish, send, delivery and acknowledgement made through a
      connection given an exchange ID carry that ID, and the anonymous forms still carry none.
- [x] A named `MessageBusOutOfSpaceTests` test shows the `FaultPoint` carries the connection's
      exchange ID, listen URL and scheme.
- [x] Every existing `Surl.MessageBus.UnitTests` test passes unchanged, the solution builds, the fast
      tests are green, and the library keeps 100% line and branch coverage.

## Notes

Filed by BL-790's run (dark factory lane 1).

2026-11-11 (lane 6): not started. `dotnet build` fails with "A compatible .NET SDK was not found": `global.json` pins 10.0.401 and the machine has only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100. Added BL-1035 (Stewart reinstalls the SDK) to `depends-on`; the board offers this task again once it is Done.

2026-11-11 (lane 1): built with the installed SDK 10.0.404 (global.json rolls forward). Decided: BusExchange(ExchangeId, ListenUrl, Scheme) + MessageBus.Connect(account, exchange); each BusConnection and BusQueueConsumer operation runs under the connection's exchange through a per-bus ThreadLocal (no static state), and a queued delivery captures the exchange that caused it, so a delivery names the publisher's exchange. Follow-up, per the task: each broker (MQTT, AMQP, Kafka, ...) passes its ExchangeContext identity to Connect; file when brokers are next planned.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. MessageBus connections take a BusExchange; bus changes, faults and out-of-space FaultPoints name it
