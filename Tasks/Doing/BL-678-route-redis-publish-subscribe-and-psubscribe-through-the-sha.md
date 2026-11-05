---
id: BL-678
title: Route Redis PUBLISH, SUBSCRIBE and PSUBSCRIBE through the shared Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-491, BL-629, BL-630]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: FR-058
created: 2026-11-04
completed:
---
# BL-678 — Route Redis PUBLISH, SUBSCRIBE and PSUBSCRIBE through the shared Surl.MessageBus

## Goal

A message a Redis client publishes on channel `a/b` reaches an MQTT, AMQP, NATS, STOMP or Kafka
subscriber of canonical topic `a/b`, and a message published on topic `a/b` over any of them reaches a
Redis `SUBSCRIBE a/b` or a matching `PSUBSCRIBE` pattern, as ADR-0100 decision 3.8 decides.

## Context

- ADR-0100 decision 3.8 (Redis pub/sub channels join the topic space): a channel that is valid
  UTF-8 without U+0000 is the canonical topic of the same text, verbatim; any other channel stays
  inside the Redis server. `PSUBSCRIBE` patterns are Redis glob patterns, which bus filters cannot
  express: the Redis server holds one bus subscription to `#` per connection with a pattern and
  matches each delivered topic against its patterns itself.
- BL-491 builds Redis pub/sub inside the server; this task moves the fan-out onto the bus.
- Specification: Redis documentation "Pub/Sub" (message and pmessage push replies). The Curl port's
  `redis` client may run only in `Integration` tests (ADR-0083 decision 1).

## Acceptance criteria

- [ ] A unit test publishes on bus topic `a/b` and a Redis connection subscribed with `SUBSCRIBE a/b`
      receives the RESP push `message`, `a/b`, payload bytes exact.
- [ ] A unit test has Redis `PUBLISH a/b x` reach a bus subscription to `a/+`, and its integer reply
      is the number of live subscriptions, of any protocol, the bus delivered it to (ADR-0100
      decision 3.8; Redis "PUBLISH": the number of clients that received the message).
- [ ] A unit test has `PSUBSCRIBE a*` receive `pmessage` for a bus publish on `a/b`, and not for `$SYS/x`.
- [ ] A channel that is not valid UTF-8 is delivered among Redis connections and never reaches the bus.
- [ ] `dotnet build` is clean and the fast tests are green; the Redis library keeps 100% line and
      branch coverage.

## Notes

- Filed by BL-622 as the follow-up ADR-0100 decision 3.8 names.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
