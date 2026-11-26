---
id: BL-1732
title: Send a retained publish once when it lands between two filters of a held MQTT SUBSCRIBE
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
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

- [x] A `Surl.Protocol.Mqtt.UnitTests` test makes a retained publish between the two filters' subscriptions and shows one `PUBLISH` sent and counted.
- [x] `dotnet build` is clean and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

- Fix: new `BusConnection.SubscribeTogether` takes all of a `SUBSCRIBE`'s filters under one hold of the
  bus's lock; `MqttHeldSubscription` uses it. A publish now comes before all of them (retained only, for
  these filters) or after all of them (live only). Recorded in ADR-0100 decision 3.3, "Several filters
  in one step" (Decided by Claude under Stewart's delegation).
- Rejected: de-duplicating in the MQTT server by remembering which messages went as retained. With three
  filters and two publishes it loses the first publish or sends the older after the newer.
- A filter already held is re-read in the same step, so a re-sent filter cannot open the gap either. A
  filter sent twice in one `SUBSCRIBE` is asked of the bus once; its return codes are as before.
- Unchanged: a publish made before a later `SUBSCRIBE` still goes live to the earlier filter and again as
  the new filter's retained message (MQTT 3.1.1 section 3.8.4 requires the resend).
- `touches` grew by `Surl.MessageBus` (the task's Context allows it) and ADR-0100; no task in Doing
  named them.
- Proof the test catches the bug: with the new lock replaced by a private one, both race tests
  (`...ARetainedPublishBetweenTwoFiltersSubscriptions...`, `...APublishMadeBetweenTwoFilters...`) failed.
- `Measure-CodeQuality.ps1`: `Surl.MessageBus` and `Surl.Protocol.Mqtt` 100% line, 100% branch, 0 failing
  members. Fast tests: 29243 passed, 0 failed, 3 skipped.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A held MQTT SUBSCRIBE subscribes its filters in one bus step, so a retained publish made meanwhile is sent and counted once
