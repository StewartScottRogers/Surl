---
id: BL-1703
title: Record and pin that a topic map hides its source name's default topic from that scheme
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests, Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1703 — Record and pin that a topic map hides its source name's default topic from that scheme

## Goal

ADR-0102 decision 3.3 and `surl --aihelp` state that a `--topic-map` alias hides, on its scheme, the topic its
name would mean by default, so a message on that topic reaches none of that scheme's subscribers (wildcards
included), and tests pin it.

## Context

- Source: Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-25T1430-Surl-bug-report.md`, item 5
  (Question), against Surl v0.1.8 `9f8005dff`. Repro, copied from the report:
  `surl --allow-anonymous --topic-map nats:orders.created=mqtt:shop/orders/new nats://127.0.0.1:P1/ mqtt://127.0.0.1:P2/`,
  a NATS subscriber to `>`, then `curl -d B mqtt://127.0.0.1:P2/orders/created`. Observed: the NATS subscriber
  receives nothing. The report asks whether this is intended, since ADR-0102 3.3 does not say.
- Decision (Claude under Stewart's delegation, 2026-11-25; this task records it as an ADR-0102 amendment):
  **intended, keep it.** Reasons:
  1. ADR-0102 3.3 already says "The left name's default mapping (`orders`) is no longer reached from that
     scheme", and decision 3.3 makes a map apply both ways: on `nats`, the subject `orders.created` now means
     `shop/orders/new`. A name must mean one topic in both directions, or a subscriber that re-publishes or
     subscribes to the subject it was handed reaches a different topic.
  2. Delivering canonical `orders/created` as `orders.created` would hand a `>` subscriber that wrong name;
     inventing another name (an escape form) would be a mapping no NATS client knows. So the topic has no NATS
     name, and a topic a scheme cannot name is not delivered, as for any topic NATS cannot name already.
  3. This is already the code: `TopicNameLookup.FromCanonicalTopic`
     (`Protocols/Messaging/Surl.MessageBus.UnitLibrary/TopicNameLookup.cs`) returns
     `TopicNameMappingOutcome.Unreachable` when the default name is aliased to another topic, and
     `NatsServerConnection.Deliver` drops an unmapped subject. The same holds for every scheme family
     (`mqtt`, `amqp`, `kafka`, `stomp`, …) and the TLS twin.
  4. A test that wants the hidden topic on that scheme adds a second map naming it
     (`nats:orders.created.raw=mqtt:orders/created`); the aihelp text says so.
- No behaviour changes, so no production code is touched. The recipe `nats-join-two-names-with-a-topic-map` is the
  Surly cookbook's to correct.
- Stand-in: the bus is the shared message store (ADR-0102); nothing new is published on the event stream.

## Acceptance criteria

- [ ] Regression test in `Protocols/Messaging/Surl.MessageBus.UnitTests/TopicNameLookupTests.cs`: with the map
      `nats:orders.created=mqtt:shop/orders/new`, `FromCanonicalTopic("nats", "orders/created")` and
      `FromCanonicalTopic("natss", "orders/created")` are `Unreachable`, `FromCanonicalTopic("nats", "shop/orders/new")`
      is `orders.created`, and `FromCanonicalTopic("mqtt", "orders/created")` is `orders/created`.
- [ ] Regression test in `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests`: a NATS connection subscribed to
      `>` on a bus with that map receives no `MSG` for a message published on canonical `orders/created`, and receives
      `MSG orders.created …` for one on `shop/orders/new`.
- [ ] `Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md`
      has an amendment (BL-1703) stating reasons 1 to 4, naming this report.
- [ ] The `--topic-map` paragraph in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` adds that the left name's own
      default topic is then not delivered to that scheme's subscribers, wildcards included, and how to reach it with a
      second map; any `Surl.Cli.UnitTests` test pinning that paragraph is updated in the same diff.
- [ ] `dotnet build -warnaserror` of `Application/Surl.Cli.UnitLibrary` is clean and the fast tests are green with no
      `TestCategory=Integration` test needed.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
