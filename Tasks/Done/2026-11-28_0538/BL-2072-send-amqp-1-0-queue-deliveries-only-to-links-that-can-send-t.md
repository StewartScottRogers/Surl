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
completed: 2026-11-28
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

- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: of two links on a `/queue/` address, a shared kept subscription and a non-durable shared one, only the one with credit is sent all four messages published, also when they were queued before either attached; while neither has credit they are ready on the bus.
- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: after one link's credit is withdrawn or drained, its session's window closes, or it holds 1000 unsettled deliveries (outcomes stated and unsettled included), another link able to send receives the next messages, none with a raised `delivery-count`.
- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: a drain on a subscription queue whose first message the filter refuses acknowledges it without using credit and sends the next one before the drain `flow`; a link's `flow` states the queue's ready count as `available`; a transactional `flow` still acquires what is sent after it.
- [x] The library's `CLAUDE.md` and `Amqp1.md` state the rule and cite the new tests, and ADR-0107 decision 5 gains a dated note pointing at ADR-0146 Amendment 7 (added lines only).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Plan: ADR-0146 Amendment 7 already decides the design, so no `protocol-architect` stage and no new ADR.
  Written in the session; `code-reviewer` reviewed the diff and found no code defect (its findings were the
  docs, written after it ran).
- `Amqp1Link` keeps the credit the consumer has left (last given, less each claim), so the bus is told only
  of a change. `Amqp1Link.StartConsumer` gives credit 0 before starting.
- `SendWaitingAsync` gives every link's consumer its credit before any link sends, and repeats rounds until
  one writes nothing, so what the bus held for a link that lost its window reaches another within the same
  frame's answer rather than waiting for the pump.
- The two non-durable shared tests the task names now grant credit before publishing. None deleted.
- Not changed, noted by the review: the loop acknowledging a subscription queue's refused backlog checks no
  cancellation (the old callback path cost the same), and `available` is looked up by queue name, so a link
  whose consumer has ended may report a re-declared queue's count (it is only a hint).
- The new tests were not run against the old code: the command that reverted the library to do so was denied.
  ADR-0146 Amendment 7 records the old behaviour.
- Results: build clean, `dotnet format` clean, fast tests green (Amqp1: 956), library 100% line and branch
  coverage, the six `Amqp1` tests in `Surl.Conformance.UnitTests` pass.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. AMQP 1.0 queue and subscription-queue messages go only to links with credit, window and room; the rest stay ready on the bus (ADR-0146 Amendment 7)
