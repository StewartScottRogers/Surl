---
id: BL-2048
title: Decide how a queue's messages reach only AMQP 1.0 links that have credit
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2040]
touches: [Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2048 — Decide how a queue's messages reach only AMQP 1.0 links that have credit

## Goal

ADR-0146 states whether a message of a queue or subscription may wait on a link with no credit while another link
consuming the same queue has some, and if not, how the container and the bus prevent it.

## Context

- Found while working BL-2040 (2026-11-27) by reading the code; nothing was run.
- `Amqp1ContainerConnection.Links.cs` (`Consume`) and `Amqp1ContainerConnection.Subscriptions.cs`
  (`MakeQueueAndConsume`) consume with 1000 unacknowledged, and the bus chooses among consumers by that room alone
  (`Surl.MessageBus.UnitLibrary`, `NamedQueue.NextConsumerWithRoomFor`). So a link that has issued no credit still
  takes its round-robin share, and those messages wait on it until it flows or ends. Prove it with a test before
  deciding.
- Links that compete: `/queue/` links, shared kept subscriptions (ADR-0146 decision 3.5) and non-durable shared
  ones (Amendment 4, BL-2041).
- A test stand-in is expected to behave as a broker does: a JMS consumer that is not receiving should not hold
  messages another consumer is waiting for. Part 2 section 2.6.7 (link flow control) is the specification to read.
- If the answer needs a `Surl.MessageBus` change, file the build tasks, bus first. Amend only by adding a dated
  amendment.

## Acceptance criteria

- [x] ADR-0146 has a dated amendment, marked "Decided by Claude under Stewart's delegation", saying what a queue's messages do when one consuming link has credit and another has none, with the alternative rejected and why.
- [x] Any build task the amendment needs is filed, bus before container, with its `depends-on` set.
- [x] The diff to ADR-0146 only adds lines.

## Notes

2026-11-28. Decision: ADR-0146 Amendment 7. A queue's message goes only to a link that can send it (credit,
session window open, under 1000 unsettled); what nobody can take stays ready. The bus gains a consumer delivery
credit with a claim at send time. Built by BL-2065 (bus), then BL-2066 (container, also after BL-2043).

Proof before deciding: a temporary MSTest file in `Surl.Extended.Protocol.Amqp1.UnitTests`
(`Amqp1ContainerConnectionTests.TemporaryCreditProbe.cs`, reusing its fakes), run with
`dotnet test ... --filter` and then deleted. Its six cases asserted today's behaviour and all passed:

- `/queue/`, shared kept and non-durable shared links, both attached, only link 1 granted credit 10, then four
  messages published: link 1 got `two`, `four`; link 0 got none.
- Four messages queued, both `/queue/` links attached, only link 1 granted credit: no transfer; the queue showed
  2 consumers and 0 ready; 4 ready again after the connection ended.
- Link 0's credit withdrawn (`flow` credit 0), and in a second case drained then set to 0: link 1 still got only
  `two`, `four`.

Not run: a case with a closed session window, and links on two connections; both follow from the code read
(`NamedQueue.NextConsumerWithRoomFor`, `Amqp1ContainerConnection.StartDelivery`), not from a test.

The amendment's account of Part 2 section 2.6.7 was written from knowledge of the specification and the
repository's citations of it; the specification text was not re-read in this run. After the amendment,
`dotnet build` was clean and the fast tests passed (30336 passed, 3 skipped, 0 failed).

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. ADR-0146 Amendment 7 decides that a queue's messages go only to AMQP 1.0 links that can send them; BL-2065 and BL-2066 build it
