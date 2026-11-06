---
id: BL-526
title: Route NATS publishes to subscriptions, queue groups and requests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-525, BL-664, BL-706]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests, Documentation/Planning/Decisions/ADR-0113-how-the-nats-server-answers-nats-and-natss.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-526 — Route NATS publishes to subscriptions, queue groups and requests

## Goal

The NATS server routes PUB and HPUB to matching subscriptions as MSG and HMSG, with wildcards, queue groups (one
member per message), `UNSUB` limits, `echo` and no-responders. It cuts off slow consumers as BL-524's ADR
says.

## Context

- Decision: BL-524's ADR, decisions 3, 4 and 7.
- The protocol page: PUB, HPUB (header and total sizes), SUB with an optional queue group, UNSUB with
  `max_msgs`, MSG and HMSG (with `reply-to`), and the subject rules (tokens separated by `.`, `*` one token, `>`
  the rest). The headers ADR: `NATS/1.0` and `NATS/1.0 503` for no responders. `-ERR 'Maximum Payload Violation'`
  for a payload over `max_payload`, and `-ERR 'Invalid Subject'`.
- Subscriptions are shared by connections and owned by the server instance, not a static.
- **Shared message bus (Stewart, 2026-11-04; BL-622's ADR, applied by BL-524's ADR):** subscriptions are the bus's,
  subjects converted with BL-630's mapping, queue groups on the bus's queues (BL-631) if BL-622's ADR says so; a
  message published over `mqtt` reaches a NATS subscriber and the reverse. The library references
  `Surl.MessageBus.UnitLibrary`. Each PUB and SUB passes the connection's account and scheme to BL-664's access
  check, a refusal answered `-ERR 'Permissions Violation for Publish to …'` / `… Subscription to …` as BL-524's
  ADR says.

## Acceptance criteria

- [x] Named tests show:
      - a PUB delivered as MSG to two subscribers;
      - `foo.*` and `foo.>` matching as the page says;
      - a queue group of two members getting one copy each of two messages between them;
      - `UNSUB 1 2` ending after two messages;
      - `echo: false` suppressing self-delivery;
      - HPUB delivered as HMSG with its headers byte for byte;
      - a request to a subject with no subscribers answered with a `503` HMSG when `no_responders` is set;
      - a payload over `max_payload` refused;
      - a slow consumer disconnected as the ADR says;
      - a message published on the bus under an MQTT name delivered as MSG on the mapped subject;
      - a PUB and a SUB refused by topic access answered as the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- The named tests, all in `NatsServerConnectionTests.Routing.cs`, in criterion order:
  `ServeAsync_PubWithTwoSubscribers_IsDeliveredAsMsgToEach`,
  `ServeAsync_StarAndFullWildcards_MatchOneTokenAndOneOrMore`,
  `ServeAsync_QueueGroupOfTwo_GivesEachMemberOneOfTwoMessages`,
  `ServeAsync_UnsubWithMaxTwo_EndsAfterTwoMessages`,
  `ServeAsync_EchoFalse_SuppressesSelfDeliveryButNotOthers`,
  `ServeAsync_Hpub_IsDeliveredAsHmsgWithItsHeaderBlockByteForByte`,
  `ServeAsync_RequestWithNoSubscriberUnderNoResponders_IsAnswered503`,
  `ServeAsync_PayloadOverMaxPayload_IsMaximumPayloadViolationAndClosed`,
  `ServeAsync_SlowConsumer_IsCutOffWithSlowConsumer`,
  `ServeAsync_MessagePublishedUnderAnMqttName_IsDeliveredAsMsgOnTheMappedSubject`,
  `ServeAsync_PubAndSubRefusedByTopicAccess_AreAnsweredPermissionsViolationAndKept`.
- Measured 2026-11-05: library 100% line, 100% branch, worst complexity and CRAP 10
  (`Measure-CodeQuality.ps1` over the Nats tests' Cobertura); 169 tests green, five repeat runs
  clean; whole-solution `dotnet build -warnaserror` clean and fast tests green.
- Design: `NatsServer(policy, bus, options)` now takes the shared `MessageBus` (BL-528 composes
  it; nothing composed it yet, so no caller outside the tests changed). Every byte a connection
  sends goes through one `NatsOutbox` with one writer, so bus deliveries never block a
  publisher and every answer keeps its order (a PUB's own MSG before its `+OK`). The bus
  connection is made for the login's user name.
- Decisions recorded in ADR-0113's new decision 8 (decided by Claude under Stewart's
  delegation): the slow consumer's `-ERR` is tried for 5 s at most; the `503` goes out in
  ordinal `sid` order; headers another protocol sends that a block cannot carry are left out;
  an HPUB with header size 0 has no native form and goes as MSG. ADR-0113 was added to
  `touches` for it: no task in Doing names it.
- `max_payload` in INFO is now the smaller of `--max-message` and the bus's `MaxMessageBytes`,
  as decision 7 says (`ServeAsync_BusBoundBelowMaxMessage_IsTheInfoMaxPayload`).
- Not done here: `Documentation/Conformance/Extended/Nats.md` still names BL-526 on its rows,
  because `Documentation/Conformance` is in BL-395's `touches` (in Doing). Filed BL-825 to
  trace them.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-664; subscriptions are Surl.MessageBus's.
- 2026-11-05: Amended by BL-524: waits on BL-706 (the `nats` native form keeps HPUB headers byte for byte, ADR-0113 decision 4).
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. NATS PUB/HPUB/SUB/UNSUB route through the shared bus: MSG/HMSG, wildcards, queue groups, UNSUB limits, echo, no-responders, topic access and slow-consumer cut-off
