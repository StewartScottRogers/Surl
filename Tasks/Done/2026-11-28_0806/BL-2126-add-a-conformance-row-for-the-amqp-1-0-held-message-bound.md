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
completed: 2026-11-28
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

- [x] A row under "Transactions" states that a posting past 100000 held messages on a connection is answered `transactional-state` with outcome `rejected` and `amqp:resource-limit-exceeded`, under every `--max-filesize`, and names the test above.

## Notes

- Added row `Amqp1:Surl-Transactions-2` after `Amqp1:Surl-Transactions-1`, so it sits above
  `Amqp1:Transactions-4.5.8-1`, which points at "the bound rows above". The `Surl-` form is the ID because the
  bound is Surl's own (ADR-0146, note to decision 1.4), not a Part 4 requirement.
- One table row, so written in the run itself rather than delegated to `align-and-document`. No `.cs` or project file
  changed.
- Checked against `Amqp1ContainerConnection.Hold` and `MessageBus.DefaultMaxMessagesPerQueue` (100000). The named
  test passes (both data rows); `dotnet build` is clean and the fast tests pass.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The AMQP 1.0 conformance matrix states the 100000 held-message bound (Amqp1:Surl-Transactions-2) and names its test.
