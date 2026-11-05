---
id: BL-651
title: Correct ADR-0093 decision 6's table to MQTT 5.0 sections 2.2.2.2, 3.8.3 and 3.10.3
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-495]
touches: [Documentation/Planning/Decisions/ADR-0093-how-the-mqtt-5-broker-answers-mqtt5-mqtt5s-and-mqtt5-ws.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-651 — Correct ADR-0093 decision 6's table to MQTT 5.0 sections 2.2.2.2, 3.8.3 and 3.10.3

## Goal

ADR-0093 decision 6's table gives each error the reason code MQTT 5.0 itself gives it, as the
codec BL-495 built already does.

## Context

- ADR-0093 decision 6's table puts "a property ... not allowed in its packet (2.2.2.2)" under
  `0x82` Protocol Error, and "an empty SUBSCRIBE or UNSUBSCRIBE payload" under `0x81`
  Malformed Packet. MQTT 5.0 says the reverse for both:
  - 2.2.2.2: "A Control Packet which contains an Identifier which is not valid for its packet
    type ... is a Malformed Packet" (`0x81`);
  - 3.8.3: "A SUBSCRIBE packet with no Payload is a Protocol Error" (`[MQTT-3.8.3-2]`, `0x82`);
  - 3.10.3: "An UNSUBSCRIBE packet with no Payload is a Protocol Error" (`[MQTT-3.10.3-2]`, `0x82`).
- 3.8.3.1 also makes a Maximum QoS or Retain Handling of 3 a Protocol Error (`0x82`); the table
  does not list them.
- The codec (`Surl.Extended.Protocol.Mqtt5.UnitLibrary`, `Mqtt5BodyReader.ReadProperties`,
  `Mqtt5SubscribePacket.Read`, `Mqtt5UnsubscribePacket.Read`, `Mqtt5Subscription.Read`) follows
  the specification; BL-495's Notes record why. BL-495 could not edit the ADR because BL-470
  held `Documentation/Planning/Decisions` at the time.

## Acceptance criteria

- [ ] ADR-0093 decision 6's table lists a property not allowed in its packet under `0x81`, an
      empty SUBSCRIBE or UNSUBSCRIBE payload and a Maximum QoS or Retain Handling of 3 under
      `0x82`, each with its section or statement ID.
- [ ] The ADR records the amendment with its date and BL-609.

## Notes

## Log

- 2026-11-04: Created.
