---
id: BL-715
title: Record the message bus's No Local publish and after-delivery hook in ADR-0100
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-498]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-05
completed:
---
# BL-715 — Record the message bus's No Local publish and after-delivery hook in ADR-0100

## Goal

ADR-0100 decision 3.3 states the three message bus additions BL-498 made for MQTT 5, so the ADR is
true of `Surl.MessageBus.UnitLibrary` again.

## Context

- BL-498 (2026-11-05) added, in `Surl.MessageBus.UnitLibrary`:
  - `BusConnection.Publish(BusMessage)`: publishes for the connection's account, marked as that
    connection's own;
  - `BusConnection.Subscribe(..., noLocal: true)` and `BusSubscription.NoLocal`: a message the same
    connection published through `BusConnection.Publish` is not handed to that subscription
    (MQTT 5.0 `[MQTT-3.8.3-3]`); one published through `MessageBus.Publish` still is;
  - `BusConnection.AfterMessageDelivered`: called once a live message has been handed to every one of
    that connection's subscriptions it reached, so a server sends a message matching several of a
    client's subscriptions once (MQTT 5.0 `[MQTT-3.3.4-2]`, ADR-0100 decision 3.3's "each server
    then applies its own overlap rule").
- BL-498 could not edit `Documentation/Planning/Decisions` while BL-423 held it.
- No Local is per bus connection, which is per client identifier while sessions are not kept; BL-499
  (sessions) decides whether a resumed session's subscriptions keep it per client identifier, as
  ADR-0093 decision 3 says for a Will.

## Acceptance criteria

- [ ] ADR-0100 decision 3.3's table (or an amendment section) names `BusConnection.Publish`, the
      `noLocal` subscribe option and `BusConnection.AfterMessageDelivered`, each with its reason.
- [ ] `Documentation/Planning/Decisions/README.md` still lists ADR-0100 with its amendment noted, if
      the ADR's header changes.

## Notes

## Log

- 2026-11-05: Created.
