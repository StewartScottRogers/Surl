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
completed: 2026-11-06
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

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by Claude
      under Stewart's delegation", answering the three items with their Part and section, and is listed in the
      folder's `README.md`.
- [x] `Documentation/Conformance/Extended/Amqp1.md` gains rows for Part 4 and the filter and durability fields,
      each naming a filed task or "n/a: <reason>".
- [x] The feature tasks that build the three are filed.

## Notes

- Filed by BL-509 (ADR-0107 decision 5).
- Wrote ADR-0146 (README row added); 0146 is the next number after the highest in this checkout (0137 is a gap another lane holds). Read from AMQP 1.0 Parts 2 to 4, the Apache filter registry (apache-filters.xml) and Jakarta Messaging 3.1 section 3.8.1; the Curl port was not read. Part 4 section numbers in this task's Context were inexact (4.2 is declaring, 4.3 discharging, 4.4 using, 4.5 the types); the ADR cites the real ones.
- Choices: transactions are connection-scoped with 4-byte ids counting from 1 (deterministic without a seed); a commit is all or none through a new bus batch publish rather than in order as AMQP 0-9-1 does, because Part 4 has no "nothing is atomic" allowance; selectors run in the container and reach the bus only as a consumer predicate, keeping the bus protocol-neutral; durable subscriptions are persisted bus queues named by SHA-256 of container id and link name, with a subscription description kept beside them, filtered at delivery.
- Amendments to ADR-0107 decisions 4 and 5 and ADR-0100 decision 3.3 are recorded in ADR-0146 only, since this task touches only the Decisions folder and Amqp1.md.
- Filed BL-954 and BL-955 (bus), BL-956 and BL-957 (transactions), BL-958 (selector), BL-959 (filters) and BL-960 (durable and shared subscriptions). Matrix rows added for Part 4, the filter set and terminus durability and expiry; Amqp1:Surl-Nodes-6 now names BL-956. Docs only: build clean, 78 fast test assemblies green.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0146 decides AMQP 1.0 transactions, source filters and durable and shared subscriptions; Amqp1.md has their rows; BL-954 to BL-960 build them
