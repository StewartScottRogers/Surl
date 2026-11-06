---
id: BL-707
title: Decide AMQP 1.0 transactions, durable topic subscriptions and source filters
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-509]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-707 — Decide AMQP 1.0 transactions, durable topic subscriptions and source filters

## Goal

An ADR decides how the AMQP 1.0 container serves the three things ADR-0107 marks "Not served yet": transactions
(a `coordinator` target, `declare`, `discharge` and the transactional delivery states), durable topic subscriptions
(a source on a `/topic/` address with `durable` above `none` and `expiry-policy` `never`) and source filters (the
`filter` map of a source, such as JMS selectors and the `apache.org` filter types). Follow-up feature tasks build
each one.

## Context

- ADR-0107 decision 5, "Not served yet", says what the container answers until then.
- AMQP 1.0 Part 4 (transactions: 4.2 coordination, 4.3 declare and discharge, 4.5 the transactional states);
  Part 3 sections 3.5.3 (source, `filter`), 3.5.5 (terminus-durability), 3.5.6 (terminus-expiry-policy) and
  3.5.8 (filter-set). The AMQP filter registry (`apache.org:selector-filter:string` and the others) as a
  convention document, not a specification.
- The bus (ADR-0100 decision 3.3) has no transactions and no subscription that lives without a connection; the ADR
  decides what it needs from `Surl.MessageBus`, and files the bus work.
- The Curl port's client sends none of the three; its code shows what a client sends, never a verdict (ADR-0083).

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by Claude
      under Stewart's delegation", answering the three items with their Part and section, and is listed in the
      folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/Amqp1.md` gains rows for Part 4 and the filter and durability fields,
      each naming a filed task or "n/a: <reason>".
- [ ] The feature tasks that build the three are filed.

## Notes

- Filed by BL-509 (ADR-0107 decision 5).

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
