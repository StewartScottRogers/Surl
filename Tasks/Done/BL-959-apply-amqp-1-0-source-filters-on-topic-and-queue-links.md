---
id: BL-959
title: Apply AMQP 1.0 source filters on topic and queue links
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-955, BL-958]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-959 — Apply AMQP 1.0 source filters on topic and queue links

## Goal

The AMQP 1.0 container applies the selector, no-local and legacy direct, topic and headers binding filters a
receiving link's source asks for, echoes the ones it applied, and leaves out the rest.

## Context

- ADR-0146 decisions 2.1, 2.3 and 2.4; AMQP 1.0 Part 3 sections 3.5.1, 3.5.3 and 3.5.8; the Apache filter
  registry's descriptors (`0x0000468C:0x00000000` to `0x00000004`).
- Uses BL-958's selector, BL-955's consumer predicate for queue links, the bus's `noLocal` subscription for
  no-local on topics, and `AmqpTopicExchangeNames` for the topic binding and the routing key.
- Today the container leaves every `filter` out of its source (ADR-0107 decision 5); the matrix row
  `Amqp1:Messaging-3.5.3-2` notes the change.
- Matrix rows: `Amqp1:Messaging-3.5.8-1` to `-3.5.8-5`, `Amqp1:Surl-Filters-1`, the BL-959 part of
  `Amqp1:Surl-Filters-4`.

## Acceptance criteria

- [x] An attach with a selector, a no-local and an unknown filter is answered with a source echoing the first
      two under the client's keys and leaving the third out; descriptors in symbol and ulong form both work.
- [x] On a topic link only messages every applied filter accepts are delivered, and filtered-out ones do not
      count against the 1000 waiting; no-local withholds the connection's own publishes.
- [x] On a queue link with two consumers whose selectors differ, each gets only what its selector accepts and a
      message neither accepts stays ready.
- [x] Direct, topic and headers bindings match the routing key and application properties as ADR-0146 decision
      2.1 says, including a message from another protocol routed by its topic.
- [x] A malformed selector refuses the attach with a null source and a closing `detach` carrying
      `amqp:invalid-field`.
- [x] Each row above names its tests in `Amqp1.md`; 100% line and branch coverage holds.

## Notes

- Filed by BL-707 (ADR-0146 decision 2).
- Built: `Amqp1SourceFilters` (reads the source's `filter` map, applies the five registry filters, echoes
  them, `Accepts` and `QueuePredicate`) and `Amqp1BusSelectorMessage` (a bus message's header, properties
  and application properties as a transfer would carry them). Wired into `Amqp1ContainerConnection.Links`:
  filters read before the node resolves, echoed via `Amqp1Terminus.Source`, applied in `OfferTopicMessage`,
  no-local through `Subscribe(noLocal:)`, queue filters as BL-955's `accepts` predicate.
- Choices taken where ADR-0146 is silent (sensible defaults, rule 1):
  - A selector entry whose value is not a string refuses the attach like a malformed one (`amqp:invalid-field`,
    `selector not valid`), because the client asked for filtering the container cannot honour.
  - A direct or topic binding whose value is not a string, a topic pattern `AmqpTopicExchangeNames.ToCanonicalFilter`
    refuses, and a headers binding that is not a map or whose `x-match` is neither `all` nor `any` (string or
    symbol) are left out, not refused, so the client decides (Part 3 section 3.5.3).
  - Headers binding follows AMQP 0-9-1's (ADR-0112): `x-` keys not matched, a null value matches the property
    being present (non-null), binaries compare by bytes, other values by `Equals`.
  - "Sent straight to a queue" is recognised as a bus message whose topic is the queue's name, which is what
    `PublishToQueue` callers put there; such a message has no routing key unless it has a `subject`.
  - A native form that does not decode reads as an empty message rather than throwing under the bus's lock.
  - Duplicate entries of one filter type are each applied (all must match) and each echoed.
- ADR-0107 decision 5's "not served yet" bullet still points at BL-707 for filters; ADR-0146's Amends already
  records the change, and ADR-0107 is outside this task's `touches`, so it is left for the align pass.
- Coverage: `Surl.Extended.Protocol.Amqp1.UnitLibrary` 100% line and branch (cobertura, 702 tests).

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. AMQP 1.0 links apply selector, no-local and direct/topic/headers binding source filters, echoing the applied ones
