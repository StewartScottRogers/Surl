---
id: BL-2072
title: Send AMQP 1.0 queue deliveries only to links that can send them
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2071, BL-2043]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md, Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2072 — Send AMQP 1.0 queue deliveries only to links that can send them

## Goal

A message of a queue or subscription queue goes only to a link that can send it, so a link with no credit never
holds one another consumer could take.

## Context

- ADR-0146 Amendment 7 (BL-2048) decides this; items 1 and 3 to 6 are the container part. BL-2071 adds
  `BusQueueConsumer.SetDeliveryCredit` and `TryClaimDelivery`.
- Start at `Amqp1ContainerConnection.Links.cs` (`Consume`, `OfferQueueDelivery`),
  `Amqp1ContainerConnection.Subscriptions.cs` (`BindAndConsume`, `OfferSubscriptionQueueDelivery`, whose filter
  check moves to the claim) and `Amqp1ContainerConnection.Deliveries.cs` (`AnswerReceiverFlowAsync`,
  `SendLinkAsync`, `StartDelivery`, `WriteLinkFlowAsync`'s `Available`). `Amqp1Link.Waiting` stays for live topic
  links only.
- AMQP 1.0 Part 2 sections 2.5.6 (session windows) and 2.6.7 (link flow control).
- Existing tests that publish before granting credit and expect two links to split the messages (in
  `Amqp1ContainerConnectionTests.NonDurableShared.cs`,
  `ServeAsync_TwoLinksOnANonDurableSharedSource_...` and `ServeAsync_OneOfTwoNonDurableSharedLinksDetaches_...`)
  expect the old behaviour: grant credit first or expect the new distribution. Delete none.
- Stand-in: the shared bus and its event stream. No new option or fault.

## Acceptance criteria

- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: of two links on a `/queue/` address, a shared kept subscription and a non-durable shared one, only the one with credit is sent all four messages published, also when they were queued before either attached; while neither has credit they are ready on the bus.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: after one link's credit is withdrawn or drained, its session's window closes, or it holds 1000 unsettled deliveries (outcomes stated and unsettled included), another link able to send receives the next messages, none with a raised `delivery-count`.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: a drain on a subscription queue whose first message the filter refuses acknowledges it without using credit and sends the next one before the drain `flow`; a link's `flow` states the queue's ready count as `available`; a transactional `flow` still acquires what is sent after it.
- [ ] The library's `CLAUDE.md` and `Amqp1.md` state the rule and cite the new tests, and ADR-0107 decision 5 gains a dated note pointing at ADR-0146 Amendment 7 (added lines only).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-28: Created.
