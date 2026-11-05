---
id: BL-497
title: Route MQTT 5 PUBLISH with QoS flows, retained messages, topic aliases and expiry
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-496, BL-664]
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
- **Amended by the shared-stores decision (Stewart, 2026-11-04; BL-622's ADR, which amends ADR-0093 decision
  0):** routing and retained messages go through the one `Surl.MessageBus` composed per `surl` run, shared with
  the original `mqtt` server and the other brokers - not a private `Mqtt5RetainedMessages`. The library references
  `Surl.MessageBus.UnitLibrary` (allowed by BL-622's ADR-0002 row); names convert with BL-630's mapping; MQTT 5
  properties ride the bus message's properties as BL-622's ADR says; each publish passes the connection's account
  and scheme to BL-664's access check, a refusal answered with PUBACK/PUBREC `0x87` Not authorized (MQTT 5.0
  section 3.4.2.1) unless BL-642's ADR says otherwise. Session state (BL-499) stays the broker's.

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
      - user properties forwarded in order;
      - a message published through the bus by another server (as `mqtt` would) delivered to an `mqtt5`
        subscriber, and an `mqtt5` retained message visible on the bus;
      - a publish refused by topic access answered as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-664; routes and retains through Surl.MessageBus.
