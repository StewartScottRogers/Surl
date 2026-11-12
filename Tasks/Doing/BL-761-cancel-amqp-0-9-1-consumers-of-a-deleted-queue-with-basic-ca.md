---
id: BL-761
title: Cancel AMQP 0-9-1 consumers of a deleted queue with basic.cancel
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-506, BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-761 — Cancel AMQP 0-9-1 consumers of a deleted queue with basic.cancel

## Goal

Deleting a queue ends every consumer of it on every AMQP 0-9-1 connection, and a client that announced `consumer_cancel_notify` in its `client-properties` capabilities receives `basic.cancel` with its consumer tag and `no-wait` set.

## Context

- ADR-0112 decision 5's `queue.delete` bullet and its Context on RabbitMQ's server-sent `basic.cancel`; matrix row `Amqp0:Extension-1`.
- BL-506 serves `queue.delete` through `BusConnection.DeleteQueue`, but nothing tells another connection's `Amqp0Consumer` that its bus queue went: the broker needs to learn of the deletion (a bus notification, or a broker-wide consumer registry by queue name in `Amqp0Broker`) and remove the consumer from its `Amqp0Channel`.
- `client-properties` is read in `Amqp0ConnectionStartOk` but its capabilities are not kept yet. If the bus needs a change, add `Protocols/Messaging/Surl.MessageBus.UnitLibrary` to `touches`.

## Acceptance criteria

- [ ] Named tests show: a consumer on another connection that announced `consumer_cancel_notify` receives `basic.cancel` (60.30) with its tag and `no-wait` set when the queue is deleted; one that did not announce it just stops receiving; the deleting connection's own consumers are ended the same way.
- [ ] The matrix row `Amqp0:Extension-1` names those tests.
- [ ] `dotnet build` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 5): parked behind BL-1035 before any code. `dotnet build` fails with "A compatible .NET SDK
  was not found": `global.json` pins 10.0.401 and the machine has only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100,
  so the build and fast-test gate cannot pass. BL-760 (same Amqp0 library, parked the same way) has uncommitted
  work stashed by the shift; starting BL-761 on top of it now would collide with that stash.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build fails
- 2026-11-11: Backlog -> Doing.
