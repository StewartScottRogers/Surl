---
id: BL-2082
title: Bound how many messages an AMQP 1.0 connection holds in open transactions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2082 — Bound how many messages an AMQP 1.0 connection holds in open transactions

## Goal

A client cannot make an AMQP 1.0 connection hold an unlimited number of messages in its open transactions by posting
empty messages.

## Context

- Found while working BL-2064 (2026-11-28) by reading the code; no test has been run. `Hold`
  (`Amqp1ContainerConnection.Transactions.cs`) bounds held bytes only, against `--max-filesize`. A transfer with an
  empty payload decodes (`Amqp1MessageSection.ReadAll` returns no sections, and `Amqp1BusMessages.ToBusMessage`
  requires no body), adds 0 to `Amqp1Transaction.HeldBytes`, and nothing caps how many messages a transaction holds.
  So under any limit the held list can grow without end, against ADR-0006's rule that every store a peer can fill is
  bounded, and against ADR-0146 decision 1.7's "keep what a transaction holds finite".
- Decide the bound (a named constant of the library for the held message count is the likely one; the bus's own
  message bounds are the thing to compare with) and record it in a dated note in ADR-0146, only adding lines. Decide
  too whether the bound applies under `--max-filesize 0`.
- Also in ADR-0146: decision 1.4 says a posting past `--max-filesize` "is answered `rejected`"; the code
  (`PostInTransaction`) answers `transactional-state` with outcome `rejected`, as ADR-0107's BL-2044 note says. The
  same note can correct it.

## Acceptance criteria

- [ ] A named unit test in `Surl.Extended.Protocol.Amqp1.UnitTests` posts empty messages in a transaction past the bound and sees the posting refused with `amqp:resource-limit-exceeded`.
- [ ] A dated note in ADR-0146 states the bound and says that a refused posting is answered `transactional-state` with outcome `rejected`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
