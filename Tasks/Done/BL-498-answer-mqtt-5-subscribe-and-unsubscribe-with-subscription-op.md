---
id: BL-498
title: Answer MQTT 5 SUBSCRIBE and UNSUBSCRIBE with subscription options and shared subscriptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-497]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests, Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] Named tests show:
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
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- BL-497 (2026-11-05) already answers SUBSCRIBE in its plain form - `Mqtt5BrokerConnection.AnswerSubscribeAsync`
  and `Subscribe`: bus subscription per filter at its Maximum QoS, `$share/` groups, SUBACK `0x8F`/`0x87`/`0x97`,
  retained after SUBACK. Build the options, identifiers, overlap rule, replacement and UNSUBSCRIBE on it.
- 2026-11-05, touches widened: No Local and the overlap rule cannot be done from the MQTT 5 library
  alone - the bus copies every message, so a server cannot tell its own publish, nor when the bus has
  finished handing one message to all of a connection's subscriptions. Added
  `Surl.MessageBus.UnitLibrary` and `.UnitTests` to `touches`; no task in Doing named either (BL-384,
  BL-423, BL-518, BL-624 checked). Additive bus API: `BusConnection.Publish` (marked as the
  connection's own), `Subscribe(..., noLocal)` / `BusSubscription.NoLocal`, and
  `BusConnection.AfterMessageDelivered`. Tests: `BusSubscriptionNoLocalTests`.
- Decisions (ADR-0093 decision 5 already decides the behaviour; these are how):
  - `Mqtt5ClientSubscriptions` keys subscriptions by the filter exactly as sent (`[MQTT-3.8.4-3]`
    "identical"). A replacement disposes the old bus subscription first, so the per-connection bound
    never refuses it; Retain Handling 1 then sends nothing.
  - Overlap: non-shared deliveries for one message are gathered and sent from `AfterMessageDelivered`
    once, at the highest granted QoS, with every identifier in subscription order, and RETAIN 1 when
    any of the matching subscriptions has Retain As Published (the spec does not say which wins; the
    flag is kept when any subscription asked to keep it). A shared subscription's delivery is never
    merged and carries its own identifier.
  - No Local is per bus connection. With no sessions kept yet (BL-499), a connection is a client
    identifier; BL-715 records the bus additions in ADR-0100 and notes the session question.
  - `0x91` Packet Identifier in use answers every filter of a SUBSCRIBE or UNSUBSCRIBE whose
    identifier is a QoS 2 inbound awaiting PUBREL (3.9.3, 3.11.3).
  - Topic access refusal stays SUBACK `0x87` Not authorized (section 3.9.3), as BL-497 answered it.
  - With UNSUBSCRIBE answered, every packet a client sends after CONNACK is answered, so the
    `DISCONNECT 0x83` "not answered yet" branch and its test are gone; what remains is `0x82`.
- No ADR written here: `Documentation/Planning/Decisions` is BL-423's while it is in Doing. Filed
  BL-715 (docs, depends on BL-498) to add the bus additions to ADR-0100.
- Tests: `Mqtt5BrokerTests.Subscribe.cs` - granted QoS and `0x8F` (seven invalid filters), `0x87`,
  `0x91`, wildcards (`#` and `+` not matching `$SYS/x`, `a/#` matching `a`), No Local and its `0x82`
  on `$share/`, Retain As Published both ways, Retain Handling 0/1/2, a Subscription Identifier on
  live and retained deliveries, overlap (QoS 1, identifiers 1 and 2), identifier 0 `0x82`, a
  `$share/g/t` group of two sharing two messages, UNSUBSCRIBE `0x00`/`0x11`/`0x8F`, replacement under
  a bound of one subscription. Measure-CodeQuality: Mqtt5 and MessageBus libraries 100% line and
  branch; no new member over complexity 10.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: subscriptions are Surl.MessageBus's, with topic access.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. MQTT 5 SUBSCRIBE honours No Local, Retain As Published, Retain Handling, Subscription Identifiers, overlap, replacement and shared groups; UNSUBSCRIBE answers UNSUBACK 0x00/0x11/0x8F
