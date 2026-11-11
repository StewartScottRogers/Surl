---
id: BL-760
title: Bound AMQP 0-9-1 deliveries per channel and connection, and recover to the original consumer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-506, BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-760 — Bound AMQP 0-9-1 deliveries per channel and connection, and recover to the original consumer

## Goal

The AMQP 0-9-1 broker bounds outstanding deliveries as `basic.qos` says - `prefetch-count` and `prefetch-size` across all of a channel's consumers, or the connection's with `global` - and `basic.recover` without `requeue` redelivers each outstanding delivery to the consumer that had it, under a new tag marked redelivered.

## Context

- ADR-0112 decision 6, the bullets on `basic.qos` and `basic.recover`; matrix rows `Amqp0:Basic-qos-1` and `Amqp0:Basic-recover-1` in `Documentation/Conformance/Extended/Amqp0.md`.
- BL-506 left both narrower (see its Notes): `prefetch-count` bounds each consumer the channel makes afterwards (`Amqp0BrokerConnection.WindowOf`, the bus consumer's `maxUnacknowledged`), `prefetch-size` and `global` are read and ignored, and `recover` with or without `requeue` requeues every outstanding delivery.
- A channel-wide window needs deliveries held on the channel past the window while still unacknowledged on the bus (as `channel.flow` already holds them in `Amqp0Channel.Held`); `prefetch-size` never stops a first delivery while nothing is outstanding; both are ignored for `no-ack` consumers.

## Acceptance criteria

- [ ] Named tests in `Amqp0BrokerConnectionTests` show: two consumers on one channel with `prefetch-count` 1 hold one delivery between them; `global` bounds two channels together; `prefetch-size` holds a second delivery whose body passes it but never the first; `recover` without `requeue` redelivers to the same consumer tag under a new delivery tag with `redelivered` set.
- [ ] The matrix rows `Amqp0:Basic-qos-1` and `Amqp0:Basic-recover-1` name those tests.
- [ ] `dotnet build` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 5): parked behind BL-1035. `dotnet build` in the repository fails with "A compatible
  .NET SDK was not found": `global.json` pins 10.0.401 and the machine has only 10.0.100-rc.1, 11.0.100-rc.2
  and 11.0.100. The pinned-SDK gate (criterion 3) cannot pass until Stewart reinstalls it.
- The work is done and left uncommitted for the shift to stash (it is not in this commit): built and run
  from a directory outside the repository, so SDK 11.0.100 picked it up, all 334 tests of
  `Surl.Extended.Protocol.Amqp0.UnitTests` pass, 7 of them new. On resuming, rerun `dotnet build` and the
  fast tests with the pinned SDK, then check coverage, then tick the boxes.
- Design (ADR-0112 decision 6 already decides the behaviour, so no new ADR): `Amqp0DeliveryWindow` is
  one qos window (count, bytes, and the two bounds); each channel has one and the connection has one for
  `global`. Deliveries to consumers that acknowledge go through `DeliverWithinWindows`
  (`Amqp0BrokerConnection.Prefetch.cs`): written when both windows have room and nothing of the channel
  already waits, otherwise kept in order as an `Amqp0WindowedDelivery`, unacknowledged on the bus. Every
  `Settle` (and the channel's release) takes its deliveries out of the windows and writes the waiting
  ones that now fit. One `windowGate` lock guards all windows and the waiting list and is always taken
  before a channel's `Gate`. The bus consumer's own window stays min(channel prefetch-count, 1000).
- Choices: an ack inside a transaction frees window room only at `tx.commit`, when the bus settles it
  (rollback puts it back in `Outstanding`, so the count stays right). `basic.cancel` rejects the
  cancelled consumer's waiting deliveries with requeue so the other consumers get them; the channel's
  close drops them from the waiting list after disposing its consumers, which puts them back. `recover`
  without `requeue` sends gets and cancelled consumers' deliveries back to their queue, because nobody
  is left to receive them, and writes the rest again ahead of every waiting delivery, then `recover-ok`.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build fails; code and 7 new tests done and stashed, green on SDK 11
