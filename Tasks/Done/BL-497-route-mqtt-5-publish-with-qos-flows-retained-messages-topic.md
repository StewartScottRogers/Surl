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
completed: 2026-11-05
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

- [x] Named tests show:
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
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- **Where each criterion is proved** (`Mqtt5BrokerTests.Publish.cs` unless named):
  - QoS flows: `ServeAsync_Publish_DeliversAtTheLowerOfTheTwoQualitiesOfService`,
    `ServeAsync_QualityOfService1_IsAcknowledgedInboundAndOutbound`,
    `ServeAsync_QualityOfService2_RunsPubrecPubrelPubcompBothWaysAndDeliversARepeatOnce` (also the duplicate QoS 2
    not delivered twice, and PUBREL unknown `0x92`), `Mqtt5OutboundDeliveriesTests`;
  - `0x93`: `ServeAsync_QualityOfService2PastTheReceiveMaximum_DisconnectsWithReceiveMaximumExceeded`;
  - aliases: `ServeAsync_TopicAliasSetThenUsed_DeliversBothOnTheAliasedTopic`,
    `ServeAsync_TopicAliasRefused_DisconnectsWithItsReasonCode` (`0x94`),
    `ServeAsync_SubscriberAllowsTopicAliases_GetsTheAliasThenAnEmptyTopicName`, `Mqtt5OutboundTopicAliasesTests`;
  - expiry: `ServeAsync_MessageExpiry_DropsAnExpiredMessageAndSendsTheIntervalLeftOnADelayedOne`;
  - `0x99`: `ServeAsync_Utf8IndicatedPayloadThatIsNot_AcknowledgesPayloadFormatInvalidAndDeliversNothing`,
    `ServeAsync_PayloadFormatRefused_DisconnectsWithItsReasonCode`;
  - retained: `ServeAsync_RetainedMessage_GoesToALaterSubscriberWithRetainSetAndAnEmptyOneClearsIt`;
  - user properties: `ServeAsync_ForwardedProperties_ArriveUnalteredWithUserPropertiesInOrder`;
  - bus: `ServeAsync_MessageAnotherServerPublishesOnTheBus_IsDeliveredToAnMqtt5Subscriber`,
    `ServeAsync_RetainedPublish_IsTheBussRetainedMessage`;
  - topic access: `ServeAsync_TopicAccessRefuses_AnswersNotAuthorizedAndDropsAQualityOfService0Publish`.
  - Coverage of `Surl.Extended.Protocol.Mqtt5.UnitLibrary`: line 1.0, branch 1.0 (cobertura from the MSTest
    coverage collector); 248 tests, five runs green.
- **Plain SUBSCRIBE added here.** BL-498 depends on BL-497, yet outbound flows, retained delivery and bus delivery
  need a subscriber. So BL-497 answers SUBSCRIBE in its plain form: each filter a bus subscription at its Maximum
  QoS (`$share/<g>/` a bus group, retained not sent to it), SUBACK `0x8F`/`0x87`/`0x97`, retained after SUBACK
  with RETAIN 1, live with RETAIN 0. No Local, Retain As Published, Retain Handling, Subscription Identifiers,
  replacing a filter and UNSUBSCRIBE (still `0x83`) stay BL-498's.
- **Choices made (Decided by Claude under Stewart's delegation)**; BL-509 held `Documentation/Planning/Decisions`
  while this ran, so BL-705 records them in ADR-0093:
  1. Account: `Accepted` publishes as the user name; `AcceptedUnchecked` as anonymous (null), which is what
     `--allow-anonymous` means to the bus's access grants.
  2. Topic access refusal is `0x87` as the task and ADR-0102 decision 3.4 say, superseding ADR-0093 decision 2's
     "never `0x87`".
  3. `StoreFull`/`TooManyTopics` are `0x97`; `MessageTooLarge` is DISCONNECT `0x95` because `0x95` is no PUBACK
     code; `0x10` when no subscription and no queue took the message.
  4. An empty Will Topic (unmappable on the bus, `[MQTT-4.7.3-1]`) is CONNACK `0x81`.
  5. Expiry sent is the time left rounded up, so a message is never sent with less than it has.
  6. At most 1000 deliveries wait per client (ADR-0006 bound), as ADR-0093 decision 3 bounds a session queue.
  7. A delivery over the client's Maximum Packet Size is dropped and noted, as `[MQTT-3.1.2-25]` requires.
  8. A delivery the client cannot name (`Unreachable` under `--topic-map`) is dropped.
  9. Wills publish from a timer, which cannot await a save; the next save writes them.
  10. Mqtt5BrokerState now takes the `MessageBus`; the `ApplicationMessagePublished` event is gone (Wills go to
      the bus). No composition referenced the broker yet (BL-501 composes it).
- No upstream curl measurement: `mqtt5` is an extended scheme (ADR-0083); every byte is written from MQTT 5.0.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-664; routes and retains through Surl.MessageBus.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. MQTT 5 PUBLISH routes through Surl.MessageBus with QoS 0/1/2 flows both ways, Receive Maximum, topic aliases, expiry, payload format, retained messages and topic access
