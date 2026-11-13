---
id: BL-670
title: Run the Curl port's amqp, stomp, nats, kafka and mqtt5 clients against messages published to surl over MQTT
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-640, BL-975, BL-507, BL-514, BL-973, BL-528, BL-533]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-670 — Run the Curl port's amqp, stomp, nats, kafka and mqtt5 clients against messages published to surl over MQTT

## Goal

`Integration` tests show the Curl port's pinned `amqp`, `amqp1`, `stomp`, `nats`, `kafka` and `mqtt5` clients
consuming, from one `surl`, a message pinned upstream curl published over `mqtt`, and upstream curl's `mqtt`
subscribe receiving a message one of the port's clients published - the shared message bus seen from both
families of client.

## Context

- Decisions: BL-622's ADR decisions 3 and 5 (name mapping and which semantics survive; planning class
  `CurlPortClientConsumesMessagesSharedAcrossSurlProtocolsTests` in `Conformance/Surl.Conformance.UnitTests`),
  ADR-0083 decisions 1 to 3 (port client pinned in `CurlPortBuilds.json`, `Integration` only, never evidence).
  Each end's expected payload and name come from the specifications and BL-622's mapping, never from the port.
- The brokers are registered by BL-501 (`mqtt5`), BL-507 (`amqp`), BL-514 (`amqp1`), BL-522 (`kafka`), BL-528
  (`nats`) and BL-533 (`stomp`); their own port-client classes come from BL-502, BL-508, BL-515, BL-523, BL-529 and
  BL-534.
- ADR-0014: upstream curl's `mqtt` subscribe is one-shot and reads the retained message, so publish first.
- A missing pinned port or upstream build marks the test Inconclusive.

## Acceptance criteria

- [x] The class exists, every test in it is `[TestCategory("Integration")]`, and a named test per broker asserts the
      consumed payload equals the payload upstream curl published to the mapped topic; one more test shows
      upstream curl's `mqtt` subscribe receiving a message the port's `mqtt5` client published.
- [x] No assertion's expected value comes from the port's output.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Delivered `CurlPortClientConsumesMessagesSharedAcrossSurlProtocolsTests` (7 `Integration` tests, all pass here):
  upstream curl publishes over `mqtt` (`-u alice:secret`), and the port consumes over `amqp`, `amqp1`, `stomp`, `nats`,
  `kafka` and `mqtt5`; one more has the port's `mqtt5` publish and upstream's `mqtt` subscribe print topic length,
  topic and payload. Expected values are the published payload, or the bytes ADR-0014 says upstream prints.
- Decision (default taken): the port's `amqp` client only runs `basic.get` on a queue and nothing binds a queue to a
  topic, so a raw AMQP 0-9-1 connection written in the test from the specification declares a queue and binds it to
  `amq.topic` with the topic's routing key before the publish.
- Decision: the port's `mqtt5` publish has RETAIN clear and upstream's `mqtt` subscribe is one-shot and reads only
  retained messages, so that test starts surl with `--topic events;retain` (ADR-0102) to retain every publish on it.
- `stomp` never ends by itself, so its client runs with `-m 8`; the publish loop repeats until that client ends.
- Measured: the port's `stomp`, `nats` and `amqp1` clients need the subscription to exist before the publish (live
  only, ADR-0100 decision 3.3); `kafka` and `mqtt5` read after it.

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-522 replaced by BL-973 (BL-522 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-06: depends-on BL-501 replaced by BL-975 (BL-501 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. 7 Integration tests: the port's amqp, amqp1, stomp, nats, kafka and mqtt5 clients consume what upstream curl published over mqtt, and upstream mqtt subscribe receives the port's mqtt5 publish
