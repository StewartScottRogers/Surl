---
id: BL-2044
title: Add dated notes to ADR-0107 decision 7 and ADR-0146 decision 1.5 for kept subscriptions and held transaction bytes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1990]
touches: [Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2044 — Add dated notes to ADR-0107 decision 7 and ADR-0146 decision 1.5 for kept subscriptions and held transaction bytes

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
- ADR-0107 decision 4's "Dynamic nodes" bullet is BL-2042's subject; leave it.

## Acceptance criteria

- [x] A dated note after ADR-0107 decision 7's table says the "deliveries waiting" row's topic wording holds for live subscriptions only, and how a kept subscription is bounded.
- [x] A dated note after the same table says held transaction messages count against `--max-filesize` and what answers a posting past it.
- [x] A dated note at ADR-0146 decision 1.5 says the sentence about topic deliveries holds for live subscriptions only, and that its reference is ADR-0107 decision 5.
- [x] The diff to both ADRs only adds lines.

## Notes

- Delivered by `align-and-document` (2026-11-28). `git diff --numstat`: ADR-0107 33 added, 0 removed;
  ADR-0146 11 added, 0 removed. Also added one "Amended" bullet to ADR-0107's header list, as
  BL-1990 and BL-2036 did for their notes.
- The code differs from Context in two ways, and the notes follow the code. A posting past the limit is
  answered `transactional-state` with outcome `rejected`, not a plain `rejected`. Non-durable shared
  subscriptions (ADR-0146 Amendment 4) are queue deliveries too, so both subscription notes name them.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: 30641 passed, 0 failed, 3 skipped.
- Found by reading the code: under `--max-filesize 0` every non-empty transactional posting is refused
  (`Hold` has no `> 0` guard; STOMP's has one). The ADR-0107 note states it; filed as BL-2063, with the
  ADR note for the fix as BL-2064. Topic-delivery comments in the library filed as BL-2062.
- The stale shared-source statements in ADR-0107's BL-1990 notes are already BL-2055's subject.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. ADR-0107 decision 7 and ADR-0146 decision 1.5 carry dated notes saying where their topic-delivery and --max-filesize statements do not hold in the code
