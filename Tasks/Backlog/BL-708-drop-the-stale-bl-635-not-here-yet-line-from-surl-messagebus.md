---
id: BL-708
title: Drop the stale BL-635 'not here yet' line from Surl.MessageBus's CLAUDE.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-635]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary]
requirement: none
created: 2026-11-05
completed:
---
# BL-708 — Drop the stale BL-635 'not here yet' line from Surl.MessageBus's CLAUDE.md

## Goal

`Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md` says the MQTT 3.1.1 server is on the bus, as it is
since BL-635.

## Context

- BL-635 moved `Surl.Protocol.Mqtt.UnitLibrary`'s retained messages onto the bus (`MqttProtocolServer` takes a
  `MessageBus`, `Surl.Composition` composes and loads one bus per run). It could not edit the bus's CLAUDE.md,
  because BL-664 held `Surl.MessageBus.UnitLibrary` in Doing at the time.
- The bus's CLAUDE.md still ends "Not here yet, each its own task: the MQTT 3.1.1 server's move onto the bus
  (BL-635), and access checks (BL-664)". Its first paragraph says "MQTT 3.1.1 today", which is now true of the
  composed server too.

## Acceptance criteria

- [ ] `Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md` no longer lists BL-635 as not here yet, and
      says the original `mqtt`/`mqtts` server keeps its retained messages on the bus (`MqttProtocolServer`,
      composed in `Surl.Composition`).
- [ ] Every other statement in that file is still true of the code (BL-664's state included).

## Notes

## Log

- 2026-11-05: Created.
