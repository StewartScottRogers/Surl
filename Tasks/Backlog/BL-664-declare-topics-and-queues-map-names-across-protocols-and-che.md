---
id: BL-664
title: Declare topics and queues, map names across protocols and check publish and subscribe access in Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-642, BL-630, BL-634]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-664 — Declare topics and queues, map names across protocols and check publish and subscribe access in Surl.MessageBus

## Goal

`Surl.MessageBus` accepts a configuration of declared topics (with retain and a maximum QoS), declared queues
(durable or not), name maps from one protocol's name to another's, and publish/subscribe grants per account, and
answers every publish and subscribe with an allowed or a typed refused outcome; with none configured it allows
everything it allows today.

## Context

- Decision: BL-642's ADR decision 3 (`--topic`, `--queue`, `--topic-map`, `--topic-access`, the default once any
  access is configured, how declared queues persist) and BL-622's ADR decision 3 (the bus).
- Where: `Protocols/Messaging/Surl.MessageBus.UnitLibrary`, using BL-630's name mapping (a `--topic-map`
  entry is applied on top of it) and BL-634's persistence for durable declared queues. Members arrive already
  expanded from groups (BL-646). Configuration by constructor.
- Callers pass their account (or anonymous) and scheme with each publish and subscribe; the MQTT server does so in
  BL-665, the extended brokers in their routing tasks.
- A denied subscribe must not reveal whether a topic exists (ADR-0006 section 3's principle).

## Acceptance criteria

- [ ] Named tests show: a declared topic's retain and QoS ceiling applied; a declared queue exists at start and a
      durable one survives a reload; `amqp:orders=mqtt:shop/orders` delivers an MQTT publish to `shop/orders` to
      an AMQP subscriber of `orders`; an account with `pub` only is refused a subscribe and the reverse; an
      unlisted account gets the ADR's default; with nothing configured every existing bus test passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
