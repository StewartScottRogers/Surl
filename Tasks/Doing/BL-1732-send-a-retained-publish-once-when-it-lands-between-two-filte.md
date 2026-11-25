---
id: BL-1732
title: Send a retained publish once when it lands between two filters of a held MQTT SUBSCRIBE
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1732 — Send a retained publish once when it lands between two filters of a held MQTT SUBSCRIBE

## Goal

A retained publish made while a held MQTT 3.1.1 `SUBSCRIBE` is still subscribing its filters is sent to that subscriber once, not twice.

## Context

- ADR-0014 Amendment 1, decision 3: each later publish is sent once per connection however many filters match.
- `MqttHeldSubscription.Subscribe` subscribes its filters one at a time, each under its own bus lock. For filters
  `a/#, a/b`, a retained publish to `a/b` between the two calls waits as a live publish (through `a/#`) and is also
  returned as `a/b`'s retained message, so it goes as `0x31` and `0x30` and counts twice against the hold count.
- The bus delivers and returns as retained the same `BusMessage` instance. If this needs an atomic multi-filter
  subscribe on `Surl.MessageBus`, add that library to `touches`.
- Found in the review of BL-1720. Plain curl sends one filter and is unaffected.

## Acceptance criteria

- [ ] A `Surl.Protocol.Mqtt.UnitTests` test makes a retained publish between the two filters' subscriptions and shows one `PUBLISH` sent and counted.
- [ ] `dotnet build` is clean and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
