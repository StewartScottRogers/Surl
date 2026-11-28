---
id: BL-2125
title: Bound the node names and transactional deliveries an AMQP 1.0 transaction keeps
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2082]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2125 — Bound the node names and transactional deliveries an AMQP 1.0 transaction keeps

## Goal

A client cannot make an open AMQP 1.0 transaction keep unlimited memory through long node names or through the
container's own deliveries.

## Context

- Found while working BL-2082 (2026-11-28) by reading the code; no test has been run.
- `MaxHeldMessages` bounds the number of posted messages held. Each also keeps its node's name (`Amqp1Link.NodeName`,
  up to about one frame), which no byte bound counts. A link attached again, with another long topic name or the same
  one, has its own string, so 100000 postings can keep several gigabytes under a nonzero `--max-filesize`.
- `Amqp1Transaction.Acquired` keeps every unsettled delivery the container sends under a `flow` naming the `txn-id`
  until the discharge, also after the client settles it outside the transaction, which frees room under the 1000
  unsettled cap. `Amqp1Transaction.HeldOutcomes` keeps every outcome of a settled transactional `disposition`; for a
  live topic subscription's deliveries nothing else bounds them.
- ADR-0006: every store a peer can fill is bounded. Decide the bounds and record them in a dated note in ADR-0146.

## Acceptance criteria

- [x] A named unit test shows that postings from links attached again with long node names are refused with `amqp:resource-limit-exceeded` past a stated bound.
- [x] A named unit test shows that a transaction's `Acquired` deliveries and held outcomes do not grow past a stated bound.
- [x] A dated note in ADR-0146 states both bounds.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Bounds, per connection across its open transactions (ADR-0146, BL-2125 notes to decisions 1.4 and 1.5):
  `MaxHeldNodeNameChars` 16 Mi characters, `MaxAcquiredDeliveries` 100000, `MaxHeldOutcomes` 100000. Held outcomes are
  kept bare, and an ended link's deliveries leave every open transaction.
- Tests, in `Amqp1ContainerConnectionTests.TransactionBounds.cs`:
  `ServeAsync_PostingsFromLinksAttachedAgainWithLongNodeNames_AreRefusedPastMaxHeldNodeNameChars`,
  `ServeAsync_AcquisitionPastMaxAcquiredDeliveries_SendsNothingMoreInTheTransactionUntilALinkEnds`,
  `ServeAsync_TransactionalDispositionPastMaxHeldOutcomes_IsRejectedWithResourceLimitExceededAndChangesNothing`.
- Verified 2026-11-28: build 0 warnings, format clean, fast tests 30888 passed, 3 skipped, 0 failed;
  `Measure-CodeQuality.ps1` on the library 100% line and branch, 0 failing members. No `Integration` code changed.
- Review (code-reviewer) found no bug in the change; its older finding, rollback growth on live topic links, is BL-2128.
- `Documentation/Conformance/Extended/Amqp1.md` is outside `touches` and has no new row for these bounds.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. Open AMQP 1.0 transactions keep at most 16 Mi characters of node names, 100000 acquired deliveries and 100000 bare held outcomes per connection
