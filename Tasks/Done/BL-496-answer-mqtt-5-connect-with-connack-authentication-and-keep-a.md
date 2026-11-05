---
id: BL-496
title: Answer MQTT 5 CONNECT with CONNACK, authentication and keep alive
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-495]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-496 — Answer MQTT 5 CONNECT with CONNACK, authentication and keep alive

## Goal

The MQTT 5 broker answers CONNECT with CONNACK carrying the properties BL-494's ADR lists. It checks the
protocol name and level, the username and password, and the Will. It assigns a client identifier when the client
sends none, enforces the keep alive at one and a half times its value, and refuses with the right reason code.

## Context

- Decision: BL-494's ADR, decisions 1, 2 and 8.
- MQTT 5.0:
  - 3.1 (CONNECT; `[MQTT-3.1.0-1]` the first packet; `[MQTT-3.1.2-1]` and `-2` on the protocol name and level,
    `0x84` Unsupported Protocol Version);
  - 3.1.2.10 and 3.1.2.11 (Keep Alive and its properties);
  - 3.1.3 (payload, Will Properties, and the zero-length Client Identifier);
  - 3.2 (CONNACK and its reason codes, Assigned Client Identifier `[MQTT-3.2.2-16]`);
  - 4.13 (errors).
- Logins go through the `Surl.Protocol.Abstractions` contracts, as `MqttConnectJudge` does for 3.1.1.
- Time from `ExchangeContext.TimeProvider`.

## Acceptance criteria

- [x] Named tests show:
      - a CONNECT with good credentials answered with CONNACK `0x00` and the ADR's properties;
      - a bad password gets `0x86`;
      - no credentials with anonymous off gets `0x87`;
      - protocol level 4 gets `0x84`;
      - a zero-length client ID gets an Assigned Client Identifier;
      - a second CONNECT on the connection closes it with `0x82`;
      - a packet before CONNECT closes the connection;
      - silence past 1.5 times the keep alive closes it with `0x8D`;
      - the Will published on an abnormal close after its delay.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Delivered in `Surl.Extended.Protocol.Mqtt5.UnitLibrary`: `Mqtt5Broker` (the server, with ADR-0093's
  `MaxSessionExpirySeconds` 3600 and `MaxKeepAliveSeconds` 60, Receive Maximum 100, Topic Alias Maximum 10),
  `Mqtt5BrokerConnection`, `Mqtt5BrokerState`, `Mqtt5ConnectJudge`, `Mqtt5ConnectAcknowledgementProperties`,
  `Mqtt5ApplicationMessage`, `Mqtt5ConnectedClient`, `Mqtt5ConnectionEnd`. `Mqtt5PacketReadResult` gained
  `RefusedPacketType`, so a refused CONNECT is still answered CONNACK with its fault's reason code and a refused
  second CONNECT is DISCONNECT `0x82`, as ADR-0093 decision 6 says.
- Tests (in `Mqtt5BrokerTests`) per acceptance item: `ServeAsync_ConnectWithGoodCredentials_AnswersSuccessWithTheAdrsProperties`,
  `ServeAsync_BadPassword_AnswersBadUserNameOrPasswordAndCloses`, `ServeAsync_LoginRefusedUnchecked_AnswersNotAuthorized`,
  `ServeAsync_ProtocolLevelNot5_AnswersUnsupportedProtocolVersion`, `ServeAsync_ZeroLengthClientIdentifier_AssignsOneInConnack`,
  `ServeAsync_SecondConnect_DisconnectsWithProtocolError`, `ServeAsync_FirstPacketNotAnAnswerableConnect_ClosesWithNoBytes`,
  `ServeAsync_SilencePastOneAndAHalfTimesTheKeepAlive_DisconnectsWithKeepAliveTimeout`,
  `ServeAsync_AbnormalCloseWithAWillDelay_PublishesTheWillAfterItsDelay`. Expected bytes are written by hand from
  MQTT 5.0 (ADR-0083).
- Choices with a sensible default (rule 1), within ADR-0093 rather than new decisions:
  - Where a Will is "published": through `Mqtt5BrokerState.ApplicationMessagePublished`. Routing to subscribers
    and retained messages is BL-497's (through the shared message bus, BL-622's ADR); the event is the seam it
    plugs into.
  - A Will is published after the lesser of its Will Delay Interval and the Session Expiry Interval in force
    (3.1.2.5); a later CONNECT for the same identifier publishes a waiting Will at once with Clean Start 1 (the
    old session ends) and discards it with Clean Start 0 (`[MQTT-3.1.3-9]`).
  - Until the tasks that answer them land, PUBLISH and its acknowledgements, SUBSCRIBE and UNSUBSCRIBE are
    DISCONNECT `0x83` Implementation specific error (MQTT 5.0's code for a valid packet this implementation cannot
    process), and an Authentication Method is CONNACK `0x8C` (no method is offered until BL-499 adds SCRAM; ADR-0093
    decision 2 already answers an unoffered method so). The broker is not composed into `surl` until BL-501, so no
    user meets either interim answer.
  - Authentication Data without an Authentication Method is CONNACK `0x82` (3.1.2.11.10).
- Follow-up filed: BL-679, session takeover with DISCONNECT `0x8E` (ADR-0093 decision 1), which neither this task's
  criteria nor BL-499's cover; it depends on BL-499's sessions.
- Coverage: `Surl.Extended.Protocol.Mqtt5.UnitLibrary` at 100% line and 100% branch (Code Coverage collector,
  cobertura); 205 tests in the project pass.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. The MQTT 5 broker answers CONNECT with CONNACK and the ADR-0093 properties, judges logins (0x86/0x87), refuses level 4 (0x84), assigns client identifiers, enforces 1.5x Keep Alive (0x8D) and publishes the Will after its delay
