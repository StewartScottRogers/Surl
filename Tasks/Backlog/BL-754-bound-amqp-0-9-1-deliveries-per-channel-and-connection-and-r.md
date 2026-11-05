---
id: BL-754
title: Bound AMQP 0-9-1 deliveries per channel and connection, and recover to the original consumer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-506]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-754 — Bound AMQP 0-9-1 deliveries per channel and connection, and recover to the original consumer

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

## Log

- 2026-11-05: Created.
