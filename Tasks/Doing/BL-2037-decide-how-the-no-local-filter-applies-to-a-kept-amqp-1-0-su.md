---
id: BL-2037
title: Decide how the no-local filter applies to a kept AMQP 1.0 subscription
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2037 — Decide how the no-local filter applies to a kept AMQP 1.0 subscription

## Goal

ADR-0146 states which publishes a kept subscription with the no-local filter withholds, and how the message bus
withholds them.

## Context

Found while working BL-1990 (2026-11-27) by reading the code; nothing was run.

- ADR-0146 decision 2.1 says that on a topic link, messages published by the same connection are not delivered,
  and that every applied entry is echoed. Decision 3 (kept subscriptions) says nothing about no-local.
- `Amqp1SourceFilters.Read` applies no-local on any topic link (`NoLocalPredicate` lets every message through and
  sets `NoLocal`), so the entry is echoed in the container's source and stored in a kept subscription's
  description. Only the live subscription passes it to the bus
  (`Amqp1ContainerConnection.Links.cs`, `Subscribe`). The consume in
  `Amqp1ContainerConnection.Subscriptions.cs`, `MakeQueueAndConsume`, passes nothing, so a kept subscriber is told
  the filter is applied and receives its own publishes. The library's `CLAUDE.md` says no-local is not applied to
  a kept subscription, and BL-960's notes intended it not to be echoed either.
- The bus knows a publisher only when it accepts a publish (`MessageBus.Accept`;
  `BusSubscription.KeepsFrom`). Queues keep no publisher, and a `never` subscription outlives its connection
  and is persisted. Root `CLAUDE.md` ("Decisions") says nothing is left out because it is hard.
- Decide: what "the same connection" means for a subscription that outlives connections (read Jakarta Messaging
  3.1 on `noLocal` for durable and shared subscriptions, and cite the section read); whether a publish made while
  no link is attached is withheld; whether the bus withholds at copy-in or at delivery; what is persisted and
  snapshotted; how a shared subscription and a transaction's batch publish behave; and what the echo says. ADR-0146
  rejected copy-in filtering for selectors because the bus would have to call protocol code: say why the chosen
  way does or does not have that cost.
- BL-2038 (bus) and BL-2039 (container) build the answer. Amend only by adding a dated amendment.

## Acceptance criteria

- [ ] ADR-0146 has a dated amendment, marked "Decided by Claude under Stewart's delegation", answering each question in Context, with the alternatives rejected.
- [ ] The amendment names the `Surl.MessageBus` operation or option BL-2038 adds.
- [ ] The bodies of BL-2038 and BL-2039 agree with the amendment (edited if they do not).
- [ ] The diff to ADR-0146 only adds lines.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
