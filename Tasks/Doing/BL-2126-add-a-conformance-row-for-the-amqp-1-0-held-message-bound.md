---
id: BL-2126
title: Add a conformance row for the AMQP 1.0 held-message bound
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2082, BL-2113]
touches: [Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2126 — Add a conformance row for the AMQP 1.0 held-message bound

## Goal

The AMQP 1.0 conformance matrix states the bound on messages held in open transactions and the test that covers it.

## Context

- BL-2082 added `MaxHeldMessages` (ADR-0146 decision 1.4's BL-2082 note) and did not edit the matrix, which BL-2113
  held at the time. Row `Amqp1:Transactions-4.4-1` names only the `--max-filesize` byte bound, and
  `Amqp1:Transactions-4.5.8-1` says the bound rows above pin what limits a transaction.
- The test is
  `Amqp1ContainerConnectionTests.ServeAsync_EmptyMessagesPastMaxHeldMessages_AreRefusedWithResourceLimitExceededUntilATransactionEnds`.

## Acceptance criteria

- [ ] A row under "Transactions" states that a posting past 100000 held messages on a connection is answered `transactional-state` with outcome `rejected` and `amqp:resource-limit-exceeded`, under every `--max-filesize`, and names the test above.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
