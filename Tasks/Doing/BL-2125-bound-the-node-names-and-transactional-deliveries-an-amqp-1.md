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
completed:
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

- [ ] A named unit test shows that postings from links attached again with long node names are refused with `amqp:resource-limit-exceeded` past a stated bound.
- [ ] A named unit test shows that a transaction's `Acquired` deliveries and held outcomes do not grow past a stated bound.
- [ ] A dated note in ADR-0146 states both bounds.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
