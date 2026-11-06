---
id: BL-960
title: Serve AMQP 1.0 durable and shared topic subscriptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-955, BL-959]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-960 — Serve AMQP 1.0 durable and shared topic subscriptions

## Goal

A topic source asking `durable` above `none` with `expiry-policy` `never` gets a durable subscription that keeps
messages while no link is attached, survives a restart, is resumed, replaced, looked up and destroyed as ADR-0146
says, and may be shared; `session-end` and `connection-close` keep one until that event.

## Context

- ADR-0146 decision 3; AMQP 1.0 Part 3 sections 3.5.3, 3.5.5 and 3.5.6; Part 2 section 2.6.6 (`closed` true
  destroys the terminus) and 2.8.15 (`amqp:resource-locked`).
- The subscription is a bus named queue (`surl-subscription-` and the hex SHA-256 of `container-id`, a zero
  byte and the link name) bound to the topic filter, carrying BL-955's subscription description (the address and
  the canonical encoding of BL-959's applied filters); filters apply at delivery.
- `Amqp1BusNodeDirectory` and `IAmqp1NodeDirectory` are where the container meets the bus.
- Today a topic source asking durability is answered `durable` `none` (ADR-0107 decision 5).
- Matrix rows: `Amqp1:Messaging-3.5.5-1`, `-3.5.5-2`, `Amqp1:Surl-Subscriptions-1` to `-3`,
  `Amqp1:Core-2.6.6-2`, `Amqp1:Messaging-3.5.6-1`.

## Acceptance criteria

- [ ] A durable attach is answered `durable` `configuration`, `expiry-policy` `never`; messages published while
      it is detached (`closed` false) are delivered when the same `container-id` and link name attach again,
      and after a save and load of the bus.
- [ ] A reattach with a different selector replaces the subscription (old messages gone); a reattach with a null
      source is answered with the stored source; a `closed` detach deletes the queue.
- [ ] A second attach while the first holds it is refused with `amqp:resource-locked`; with `shared` both links
      compete, keyed by the name before `|`, and `global` drops the `container-id` from the key.
- [ ] `session-end` and `connection-close` keep an unpersisted subscription until that event, answered
      `timeout` 0; `link-detach` is answered `durable` `none`.
- [ ] Topic access is judged at every attach, `/queue/surl-subscription-...` is refused with `amqp:not-found`,
      and the bus's queue bound gives `amqp:resource-limit-exceeded`.
- [ ] Each row above names its tests in `Amqp1.md`; 100% line and branch coverage holds.

## Notes

- Filed by BL-707 (ADR-0146 decision 3).

## Log

- 2026-11-06: Created.
