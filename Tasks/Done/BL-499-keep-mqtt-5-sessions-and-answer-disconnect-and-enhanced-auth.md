---
id: BL-499
title: Keep MQTT 5 sessions and answer DISCONNECT and enhanced AUTH
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-498, BL-416]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-499 — Keep MQTT 5 sessions and answer DISCONNECT and enhanced AUTH

## Goal

The MQTT 5 broker keeps a session across connections for its Session Expiry Interval, queues QoS 1 and 2 messages
for it, and resumes it with Session Present. It answers DISCONNECT with each reason code both ways. It runs
enhanced authentication and re-authentication with the AUTH packet for the methods BL-494's ADR offers.

## Context

- Decision: BL-494's ADR, decisions 2, 3 and 6.
- MQTT 5.0:
  - 4.1 (session state);
  - 3.1.2.4 (Clean Start) and 3.1.2.11.2 (Session Expiry Interval, and changing it on DISCONNECT, `0x82` if it
    was zero);
  - 3.14 (DISCONNECT, its reason codes, and `0x04` Disconnect with Will Message);
  - 3.15 and 4.12 (AUTH and enhanced authentication: Authentication Method and Data, `0x18` Continue
    authentication, `0x19` Re-authenticate, and `0x8C` Bad authentication method);
  - 4.13 (the server's DISCONNECT before closing on a protocol error).
- SCRAM exchanges: `Surl.Authentication` (BL-416), reached through the SASL contracts in `Surl.Protocol.Abstractions`.
- Time from `ExchangeContext.TimeProvider`.

## Acceptance criteria

- [x] Named tests show:
      - a session resumed with Session Present 1 and its queued messages delivered;
      - Clean Start discarding it;
      - a session expiring after its interval;
      - DISCONNECT `0x04` publishing the Will;
      - DISCONNECT `0x00` not publishing it;
      - a DISCONNECT raising a zero Session Expiry Interval treated as a Protocol Error;
      - a SCRAM-SHA-256 enhanced authentication completed with `0x18` steps;
      - re-authentication with `0x19`;
      - an unknown method gets `0x8C`;
      - a protocol error answered with the server's DISCONNECT and its reason code.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Plan: a `Mqtt5Session` (subscriptions on its own `BusConnection`, waiting-message queue,
  in-flight PUBLISHes with their sending order, QoS 2 identifiers awaiting PUBREL) kept by
  `Mqtt5BrokerState` on the context's `TimeProvider`; `Mqtt5OutboundDeliveries` stays per
  connection over the session and resends what was in flight first; `Mqtt5EnhancedAuthentication`
  runs SCRAM through `ISaslAuthenticationPolicy`, which `Mqtt5Broker` takes as an optional third
  constructor argument (no composition constructs the broker yet; BL-501 wires it).
- The acceptance tests, in `Mqtt5BrokerTests`:
  `ServeAsync_CleanStart0WithAKeptSession_ResumesItWithSessionPresentAndDeliversItsQueuedMessages`,
  `ServeAsync_CleanStart1WithAKeptSession_DiscardsIt`, `ServeAsync_KeptSessionPastItsExpiry_EndsIt`,
  `ServeAsync_DisconnectWithWillMessage_PublishesTheWillAfterItsDelay`,
  `ServeAsync_NormalDisconnectWithAKeptSession_DoesNotPublishTheWill` (and the earlier
  `ServeAsync_NormalDisconnect_DiscardsTheWill`),
  `ServeAsync_DisconnectRaisingASessionExpiryOf0_DisconnectsWithProtocolErrorAndPublishesTheWill`,
  `ServeAsync_ScramSha256Authentication_CompletesWithContinueAuthenticationSteps`,
  `ServeAsync_Reauthentication_ContinuesWithContinueAuthenticationAndEndsWithAuthSuccess`,
  `ServeAsync_UnknownAuthenticationMethod_AnswersBadAuthenticationMethod`, and
  `ServeAsync_RefusedPacketAfterConnack_DisconnectsWithItsReasonCode` /
  `ServeAsync_SecondConnect_DisconnectsWithProtocolError` (from BL-496, still passing). Also
  `ServeAsync_ResumedSessionWithAPublishUnacknowledged_ResendsItWithDup`.
- The SCRAM tests run a scripted `UnitTestSaslAuthenticationPolicy` with RFC 7677's message
  shapes, as the Kafka tests do; they pin the MQTT framing of each step, and `Surl.Authentication`'s
  own tests pin SCRAM itself. Test project keeps no reference to `Surl.Authentication`.
- Choices made under Stewart's delegation (recorded here and in the library's `CLAUDE.md`, because
  BL-435 held `Documentation/Planning/Decisions` in `Doing`; BL-743 puts them into ADR-0093):
  a session resumes only for the account that left it; a session another connection holds is
  never resumed (BL-679 adds takeover); the queue bounds apply connected or not, and a new
  session with an expiry past 1000 kept sessions is CONNACK `0x97`; absent Authentication Data is
  an empty client-first, which SCRAM refuses; SCRAM's server-final is carried in CONNACK or AUTH
  `0x00` by answering the policy's second challenge with the empty response, as `KafkaSaslLogin`
  does; re-authentication for another account is DISCONNECT `0x87`. The head timeout spans the
  whole CONNECT exchange, enhanced authentication included (ADR-0093 decision 2).
- Coverage: `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Mqtt5.UnitLibrary` reports
  100% line and 100% branch. It still lists three members over its own complexity count of 10 -
  `Mqtt5InboundPublishes.ResolveAlias` (18), `Mqtt5BusMessages.PropertiesOf` (16) and
  `Mqtt5InboundPublishes.Judge` (12) - all from BL-497 and untouched here; the build's CA1502 gate
  passes. Every method this task added or changed is at 10 or below.
- Follow-up filed: BL-743 (record the choices above in ADR-0093).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The MQTT 5 broker keeps sessions for their expiry, resumes them with Session Present 1, answers DISCONNECT both ways, and runs SCRAM enhanced authentication and re-authentication with AUTH
