---
id: BL-2025
title: Apply the no-local filter to a kept AMQP 1.0 subscription
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2024]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2025 — Apply the no-local filter to a kept AMQP 1.0 subscription

## Goal

A durable, shared, `session-end` or `connection-close` AMQP 1.0 subscriber that is echoed the no-local filter
does not receive the publishes ADR-0146's amendment (BL-2023) says it withholds.

## Context

- Found while working BL-1990 (2026-11-27) by reading the code; prove it with a failing test first.
- The entry is echoed (`Amqp1SourceFilters.NoLocalPredicate`, `Amqp1Terminus.Source`) but
  `Amqp1ContainerConnection.Subscriptions.cs`, `MakeQueueAndConsume`, never passes it to the bus, against
  ADR-0146 decision 2.1. Use the operation BL-2024 adds.
- A lookup attach (no source) is answered the stored filters (`LookKeptSubscriptionUp`), so they must keep
  matching what is applied.
- Existing live-link test:
  `Amqp1ContainerConnectionTests.ServeAsync_TopicLinkWithNoLocal_WithholdsTheConnectionsOwnPublishesOnly`.
  Matrix row `Amqp1:Messaging-3.5.8-3` in `Documentation/Conformance/Extended/Amqp1.md`.
- Stand-in: the shared message bus. No new event or fault.

## Acceptance criteria

- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: a kept subscription with no-local does not receive what the amendment withholds and does receive another connection's publish; the test failed before the change (or Notes say why this is no defect).
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: a kept subscription without the filter receives its own connection's publish.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: the amendment's reattach case (a later attach resuming the subscription) behaves as the amendment says.
- [ ] The library's `CLAUDE.md` no longer says no-local is not applied to a kept subscription and states the rule.
- [ ] Row `Amqp1:Messaging-3.5.8-3` cites the new tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
