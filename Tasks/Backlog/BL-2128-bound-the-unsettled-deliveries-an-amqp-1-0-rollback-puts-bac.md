---
id: BL-2128
title: Bound the unsettled deliveries an AMQP 1.0 rollback puts back on a live topic link
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2125]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2128 — Bound the unsettled deliveries an AMQP 1.0 rollback puts back on a live topic link

## Goal

Repeating transactional retirement and rollback cannot make an AMQP 1.0 connection keep more unsettled deliveries
than a stated bound.

## Context

- Found by the code review of BL-2125 (2026-11-28) by reading the code; no test has been run.
- A transactional `disposition` that settles removes the delivery from `Amqp1Session.Unsettled` and frees room under
  the link's 1000 (`MaxWaitingDeliveries`). `RollBack` in `Amqp1ContainerConnection.Transactions.cs` adds it back.
- On a live topic link (no bus consumer, so no bus cap of 1000 unacknowledged), a client can receive 1000, retire them
  settled in a transaction, repeat up to `MaxHeldOutcomes` (100000) and discharge with `fail` true. The link then holds
  100000 unsettled until it ends, and the client, which settled them, never names them again. Each further link can
  repeat it.
- ADR-0006: every store a peer can fill is bounded. ADR-0146 decision 1.5 and Amendments 1 and 10 say what a rollback
  does; record the bound there in a dated note.

## Acceptance criteria

- [ ] A named unit test shows that rounds of settled transactional retirement and rollback on a live topic link leave no more unsettled deliveries than the stated bound.
- [ ] A dated note in ADR-0146 states the bound.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
