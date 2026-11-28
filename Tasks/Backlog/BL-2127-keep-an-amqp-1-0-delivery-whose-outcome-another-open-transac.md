---
id: BL-2127
title: Keep an AMQP 1.0 delivery whose outcome another open transaction holds through a rollback
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2127 — Keep an AMQP 1.0 delivery whose outcome another open transaction holds through a rollback

## Goal

A delivery acquired in one transaction, whose outcome a second open transaction holds, stays with its link when the
first rolls back, so the second transaction's discharge still decides what happens to it.

## Context

- Found by the code review of BL-2114 (2026-11-28), by reading the code. No test has run it, so confirm it first.
- `RollBack` (`Amqp1ContainerConnection.Transactions.cs`) skips an acquired delivery only when an outcome has been
  applied. One held in another open transaction leaves the unsettled set and is requeued, so that transaction's
  commit changes nothing, and a held `modified` with `undeliverable-here` does not stop redelivery to the link.
- ADR-0146 decision 1.5 says a delivery with a held outcome is "neither redelivered nor dropped until the
  transaction is discharged". AMQP 1.0 Part 4 section 4.4.2 also says the delivery must not be associated with a
  different undischarged transaction than the outcome, and 4.4.4 that the outcome must be in the acquiring
  transaction, so the client in this case breaks the specification; decide the answer and amend ADR-0146.

## Acceptance criteria

- [ ] A test in `Surl.Extended.Protocol.Amqp1.UnitTests` acquires a queue delivery in a transaction, holds `accepted` for it in a second, rolls back the first, commits the second, and asserts the message is gone from the queue, or what the amended ADR-0146 says instead.
- [ ] A test does the same with `modified` with `undeliverable-here` true and asserts the link is not sent the message again, or what the amended ADR-0146 says instead.
- [ ] ADR-0146 states what the first transaction's rollback does with such a delivery.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-28: Created.
