---
id: BL-678
title: Route Redis PUBLISH, SUBSCRIBE and PSUBSCRIBE through the shared Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-491, BL-629, BL-630]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests, Documentation/Planning/Decisions]
requirement: FR-058
created: 2026-11-04
completed: 2026-11-05
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

- [x] A unit test publishes on bus topic `a/b` and a Redis connection subscribed with `SUBSCRIBE a/b`
      receives the RESP push `message`, `a/b`, payload bytes exact.
- [x] A unit test has Redis `PUBLISH a/b x` reach a bus subscription to `a/+`, and its integer reply
      is the number of live subscriptions, of any protocol, the bus delivered it to (ADR-0100
      decision 3.8; Redis "PUBLISH": the number of clients that received the message).
- [x] A unit test has `PSUBSCRIBE a*` receive `pmessage` for a bus publish on `a/b`, and not for `$SYS/x`.
- [x] A channel that is not valid UTF-8 is delivered among Redis connections and never reaches the bus.
- [x] `dotnet build` is clean and the fast tests are green; the Redis library keeps 100% line and
      branch coverage.

## Notes

- Filed by BL-622 as the follow-up ADR-0100 decision 3.8 names.
- Plan, as built: `RedisPubSub` takes the `MessageBus`. `RedisChannelNames.TryGetTopic` says
  which channels are bus topics (valid UTF-8 and `CanonicalTopicName.IsValidTopic`). Each topic
  channel is one bus subscription. A connection with patterns holds one `#` subscription and sends its
  `pmessage`s from `BusConnection.AfterMessageDelivered`, so they follow the `message` as in Redis.
  `PUBLISH` replaces each `#` subscription in the bus's count with that connection's number of
  matching patterns. Non-topic channels and local pattern matching work as BL-491 left them.
- Decisions recorded in ADR-0126 (Decided by Claude under Stewart's delegation): channels with
  `+`, `#`, the empty channel or more than 65535 bytes stay local, like non-UTF-8 ones, because
  verbatim they would be wildcards or no topic at all. Bus refusals are `-NOPERM …` (grants),
  `-OOM …` (bus bounds) and `-ERR max number of subscriptions reached`. A refused subscribe
  rolls back the whole command. The `BusConnection` carries the login held at the first bus
  subscription; a publish uses the current login. `RedisServer(policy)` keeps a bus of its own, and
  `RedisServer(policy, bus)` is the one composition will use.
- Touches widened to `Documentation/Planning/Decisions` for ADR-0126 and its index row. No task
  in Doing names it.
- Not done here: the composition does not compose Redis yet (BL-492), and `MessageBus.Names` has no
  `redis` mapping, so `--topic-map` does not apply to Redis. Filed as BL-855.
- Tests: 11 new tests in `RedisPubSubTests` plus constructor null checks in `RedisServerTests`;
  Redis tests 374 pass, and the library is at 100% line and 100% branch coverage (cobertura from
  `dotnet test --collect "Code Coverage"`).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Redis PUBLISH, SUBSCRIBE and PSUBSCRIBE ride the shared MessageBus: topic channels reach and hear every messaging protocol, others stay local (ADR-0126)
