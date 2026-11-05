---
id: BL-498
title: Answer MQTT 5 SUBSCRIBE and UNSUBSCRIBE with subscription options and shared subscriptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-497]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-498 — Answer MQTT 5 SUBSCRIBE and UNSUBSCRIBE with subscription options and shared subscriptions

## Goal

The MQTT 5 broker answers SUBSCRIBE with SUBACK and UNSUBSCRIBE with UNSUBACK. It honours each subscription's
QoS, No Local, Retain As Published and Retain Handling. It tags deliveries with Subscription Identifiers, matches
wildcards without matching `$` topics at the first level, and shares `$share/<group>/` subscriptions among their
members.

## Context

- Decision: BL-494's ADR, decision 5.
- MQTT 5.0:
  - 3.8 and 3.9 (SUBSCRIBE options and SUBACK reason codes, such as `0x8F` Topic Filter invalid and `0x9E`
    Shared Subscriptions not supported);
  - 3.10 and 3.11 (UNSUBSCRIBE and UNSUBACK, `0x11` No subscription existed);
  - 4.7 (topic names and filters, `[MQTT-4.7.1-…]` and `[MQTT-4.7.2-1]` on `$` topics);
  - 4.8 (shared subscriptions);
  - 3.8.2.1.2 (Subscription Identifier, and `0xA1` if not supported).
- The 3.1.1 server's `MqttTopicFilter` may be copied, not referenced.
- **Shared message bus (Stewart, 2026-11-04; BL-622's ADR):** subscriptions are the bus's subscriptions, filters
  converted with BL-630's mapping rather than a private filter type, and `$share/` groups use the bus's queues
  (BL-631) if BL-622's ADR says so. Each subscribe passes the connection's account and scheme to BL-664's access
  check; a refused filter gets SUBACK `0x87` Not authorized (MQTT 5.0 section 3.9.3) unless BL-642's ADR says
  otherwise.

## Acceptance criteria

- [ ] Named tests show:
      - each SUBACK reason code for its cause;
      - `#` and `+` matching, with `$SYS/x` not matched by `#`;
      - No Local suppressing a client's own message;
      - Retain As Published keeping the RETAIN flag;
      - Retain Handling 0, 1 and 2 each sending retained messages as specified;
      - a Subscription Identifier carried on delivery;
      - two members of a `$share/g/t` group each getting one of two messages;
      - UNSUBSCRIBE of an unknown filter gets `0x11`;
      - an invalid filter gets `0x8F`;
      - a filter refused by topic access gets the ADR's reason code.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: subscriptions are Surl.MessageBus's, with topic access.
