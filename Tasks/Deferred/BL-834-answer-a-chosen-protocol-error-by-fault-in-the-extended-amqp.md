---
id: BL-834
title: Answer a chosen protocol error by fault in the extended AMQP 0-9-1, AMQP 1.0, Kafka, MQTT 5 and NATS servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-062
created: 2026-11-05
completed:
---
# BL-834 — Answer a chosen protocol error by fault in the extended AMQP 0-9-1, AMQP 1.0, Kafka, MQTT 5 and NATS servers

## Goal

An `error` fault rule naming `amqp`, `amqps`, `amqp1`, `amqp1s`, `kafka`, `kafkas`, `mqtt5`, `mqtt5s`, `mqtt5+ws`,
`nats` or `natss` (or a listener of one) makes that broker answer the matching operation with the chosen protocol
error, instead of being refused at configuration.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 4
  and 9 and its extended reply table: AMQP 0-9-1 a reply code from the specification's exception list and text
  (channel or connection exception as the code's class says); AMQP 1.0 an error condition symbol and description
  (on the disposition or detach the operation calls for); Kafka an error code 1 to 127 in the request's own
  response; MQTT 5 a reason code 128 to 255, as decision 4 says for `mqtt` (QoS 0 `PUBLISH` ends with
  `DISCONNECT`); NATS `-ERR '<text>'`. `command` is the method, performative, API key name, packet type or
  protocol verb; `path` is the exchange, queue, address, topic or subject.
- Each broker is judged by its own specification (ADR-0083); unit tests replay bytes written from it. Consult
  `ExchangeContext.Faults` per operation after login; publish `FaultAppliedEvent` (BL-823's contracts and doubles).
- `Surl.Cli`: add these schemes' reply shapes to BL-785's `--fault` reply check.

## Acceptance criteria

- [ ] Named tests per broker: an operation matching an `error` rule gets exactly the chosen error, a non-matching
      one is served, and one `FaultAppliedEvent` is published.
- [ ] Named `Surl.Cli` tests: one accepted and one refused `reply` per scheme family.
- [ ] With no rule, every existing test of the touched libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the touched libraries keep 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Filed by BL-822 (ADR-0124 decision 9); depends on BL-785 and touches `Surl.Cli` for the reply shapes.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Blocked. Stewart: Surl dark factory timed out after 120 min; see Z:\repos\Surl.logs\BL-834-20261106-033059-L4.jsonl
- 2026-11-06: Blocked -> Deferred. Split into BL-951, BL-952, BL-953, BL-954, BL-955, BL-956 (timed out after 120 min; Stewart asked for the split 2026-11-06)
