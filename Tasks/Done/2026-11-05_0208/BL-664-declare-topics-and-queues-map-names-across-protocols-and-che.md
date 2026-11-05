---
id: BL-664
title: Declare topics and queues, map names across protocols and check publish and subscribe access in Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-642, BL-630, BL-634]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-664 — Declare topics and queues, map names across protocols and check publish and subscribe access in Surl.MessageBus

## Goal

`Surl.MessageBus` accepts a configuration of declared topics (with retain and a maximum QoS), declared queues
(durable or not), name maps from one protocol's name to another's, and publish/subscribe grants per account, and
answers every publish and subscribe with an allowed or a typed refused outcome; with none configured it allows
everything it allows today.

## Context

- Decision: BL-642's ADR decision 3 (`--topic`, `--queue`, `--topic-map`, `--topic-access`, the default once any
  access is configured, how declared queues persist) and BL-622's ADR decision 3 (the bus).
- Where: `Protocols/Messaging/Surl.MessageBus.UnitLibrary`, using BL-630's name mapping (a `--topic-map`
  entry is applied on top of it) and BL-634's persistence for durable declared queues. Members arrive already
  expanded from groups (BL-646). Configuration by constructor.
- Callers pass their account (or anonymous) and scheme with each publish and subscribe; the MQTT server does so in
  BL-665, the extended brokers in their routing tasks.
- A denied subscribe must not reveal whether a topic exists (ADR-0006 section 3's principle).

## Acceptance criteria

- [x] Named tests show: a declared topic's retain and QoS ceiling applied; a declared queue exists at start and a
      durable one survives a reload; `amqp:orders=mqtt:shop/orders` delivers an MQTT publish to `shop/orders` to
      an AMQP subscriber of `orders`; an account with `pub` only is refused a subscribe and the reverse; an
      unlisted account gets the ADR's default; with nothing configured every existing bus test passes unchanged.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Tests: `MessageBusConfigurationTests` (`Publish_OnADeclaredRetainTopic_IsRetainedWithoutTheRetainFlag`,
  `Publish_OnADeclaredTopicWithAQosCeiling_IsCarriedAtNoMoreThanTheCeiling`,
  `Constructor_DeclaredQueues_ExistAtStartNeverExclusiveAndTakeMessages`,
  `LoadAsync_ADurableDeclaredQueue_SurvivesAReloadWithItsMessages`), `TopicNameLookupTests`
  (`TopicMap_AmqpOrdersToMqttShopOrders_DeliversAnMqttPublishToAnAmqpSubscriberOfOrders`),
  `TopicAccessPolicyTests` (`Subscribe_AnAccountWithPubOnly_IsAccessDenied`,
  `Publish_AnAccountWithSubOnly_IsAccessDeniedAndNothingIsRetainedDeliveredOrLogged`,
  `PublishAndSubscribe_AnUnlistedOrAnonymousCaller_IsAccessDeniedOnceAnyGrantIsConfigured`). Every one of
  the 393 existing bus tests passes unchanged (no existing test file changed); 427 in all. Coverage measured with
  `--collect "Code Coverage;Format=cobertura"`: no uncovered line or branch in the library.
- Choices (Claude, under Stewart's delegation; ADR edit filed as BL-704 because BL-487 holds
  `Documentation/Planning/Decisions`):
  - The caller is a plain account name (`null` = anonymous), not a caller-plus-scheme type: ADR-0102 3.4's access
    is global, so the scheme matters only to name mapping, and servers pass it to `TopicNameLookup` there. The
    parameterless `Connect()`, `Publish(message)` and `PublishToQueue(name, message)` are anonymous, so a
    server not yet passing an account is refused once access is configured rather than let through.
  - `TopicNameLookup` is a sealed class exposed as `MessageBus.Names`, not an interface: one implementation, and
    servers get it from the bus they already hold. It knows the messaging schemes in ADR-0083's table; Redis
    (BL-678) is left out until its channel naming is decided. STOMP and AMQP 1.0 aliases name `/topic/<t>`
    addresses.
  - New `MessageBusOutcome.AccessDenied` (appended last). Deleting a declared queue also answers it, as
    ADR-0102 3.2's `403 ACCESS_REFUSED` says.
  - Access is checked before existence (unknown queue, locked queue), so a refusal reveals nothing (ADR-0006 3).
    Unbinding needs no grant: it only narrows what a queue receives.
  - `$queue/<name>` treats the name as one level whatever it holds, so `$queue/+` opens every queue and no name
    holding `/` can escape a literal grant.
  - Declarations past `MaxTopics`/`MaxQueues`, or a queue declared twice, throw `ArgumentException` at
    construction (the composition validates options first). `LoadAsync` counts declared topics and queues with
    the saved ones against the bounds, and accepts a bus holding only its still-empty declarations; a declared
    queue persisted with other flags is made again empty.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.MessageBus takes declared topics and queues, --topic-map aliases through MessageBus.Names, and --topic-access checks answered AccessDenied
