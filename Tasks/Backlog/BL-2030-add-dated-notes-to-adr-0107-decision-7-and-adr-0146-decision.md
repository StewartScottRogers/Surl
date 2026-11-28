---
id: BL-2030
title: Add dated notes to ADR-0107 decision 7 and ADR-0146 decision 1.5 for kept subscriptions and held transaction bytes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1990]
touches: [Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2030 — Add dated notes to ADR-0107 decision 7 and ADR-0146 decision 1.5 for kept subscriptions and held transaction bytes

## Goal

A reader of ADR-0107 decision 7 and ADR-0146 decision 1.5 learns from dated notes where those statements do not
hold in the code.

## Context

Found while working BL-1990 (2026-11-27). Add notes in the form BL-1990's use ("*Amended <date> (BL-###),
pointing at ...:*"); recorded text is never rewritten. The code is in
`Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary`.

- **ADR-0107 decision 7's table**, row "deliveries waiting on a sending link": "topic buffer" and "the message is
  dropped for that link (topic)" hold for a live subscription only. A link on a kept subscription consumes a bus
  queue with `maxUnacknowledged` 1000 and the bus holds the rest (`Amqp1ContainerConnection.Subscriptions.cs`,
  `MakeQueueAndConsume`, `OfferKeptDelivery`).
- **ADR-0107 decision 7's table**, row `--max-filesize`: it names one message and the partial deliveries only.
  Messages held in open transactions count too (ADR-0146 decision 1.4): in
  `Amqp1ContainerConnection.Transactions.cs`, `Hold`, a posting that takes held plus partial plus its own bytes
  past the limit is answered `rejected` with `amqp:resource-limit-exceeded`, with the connection left open. The
  partial-delivery check in `Amqp1ContainerConnection.Transfers.cs` is separate: it compares the partial bytes
  alone, without held bytes, with the bus's `MaxMessageBytes` (which `Surl.Composition`'s
  `SurlServerComposition` sets from `--max-filesize`) and closes the connection past it.
- **ADR-0146 decision 1.5**, "Topic deliveries have nothing to return: their outcomes only settle them (ADR-0107
  decision 6)": false for a kept subscription, whose deliveries are queue deliveries that a rollback returns; and
  the statement referred to is in ADR-0107 decision **5**, not 6.
- ADR-0107 decision 4's "Dynamic nodes" bullet is BL-2028's subject; leave it.

## Acceptance criteria

- [ ] A dated note after ADR-0107 decision 7's table says the "deliveries waiting" row's topic wording holds for live subscriptions only, and how a kept subscription is bounded.
- [ ] A dated note after the same table says held transaction messages count against `--max-filesize` and what answers a posting past it.
- [ ] A dated note at ADR-0146 decision 1.5 says the sentence about topic deliveries holds for live subscriptions only, and that its reference is ADR-0107 decision 5.
- [ ] The diff to both ADRs only adds lines.

## Notes

## Log

- 2026-11-27: Created.
