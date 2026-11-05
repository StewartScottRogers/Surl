---
id: BL-497
title: Route MQTT 5 PUBLISH with QoS flows, retained messages, topic aliases and expiry
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-496]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-497 — Route MQTT 5 PUBLISH with QoS flows, retained messages, topic aliases and expiry

## Goal

The MQTT 5 broker accepts PUBLISH at QoS 0, 1 and 2 with the PUBACK, PUBREC, PUBREL and PUBCOMP flows, and
delivers to matching subscribers at the lower of the two QoS levels. It honours Receive Maximum both ways, topic
aliases both ways, Message Expiry Interval, the Payload Format Indicator and retained messages, and forwards the
properties the specification says to forward unchanged.

## Context

- Decision: BL-494's ADR, decisions 4 and 8.
- MQTT 5.0:
  - 3.3 (PUBLISH, its properties, `[MQTT-3.3.2-…]` on topic aliases, and forwarding Payload Format Indicator,
    Response Topic, Correlation Data, User Properties and Content Type unaltered);
  - 3.4 to 3.7 (the acknowledgements and their reason codes, such as `0x10` No matching subscribers);
  - 4.3 (QoS flows);
  - 4.4 (redelivery on reconnect);
  - 4.6 (ordering);
  - 4.9 (flow control: `0x93` Receive Maximum exceeded).
- Delivery to another connection's subscriber goes through broker state shared by connections (as
  `MqttRetainedMessages` is for 3.1.1), owned by the server instance, not a static.

## Acceptance criteria

- [ ] Named tests show:
      - each QoS flow inbound and outbound;
      - a duplicate QoS 2 PUBLISH not delivered twice;
      - a PUBLISH past the client's Receive Maximum gets `0x93`;
      - a topic alias set and then used;
      - an alias over the maximum gets `0x94`;
      - an expired message not delivered;
      - the remaining expiry interval sent on a delayed delivery;
      - a Payload Format Indicator of 1 with invalid UTF-8 gets `0x99`;
      - a retained message delivered to a later subscriber and cleared by an empty retained PUBLISH;
      - user properties forwarded in order.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
