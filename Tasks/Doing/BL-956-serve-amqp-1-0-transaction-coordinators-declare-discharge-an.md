---
id: BL-956
title: Serve AMQP 1.0 transaction coordinators, declare, discharge and transactional posting
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-961]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-956 — Serve AMQP 1.0 transaction coordinators, declare, discharge and transactional posting

## Goal

The AMQP 1.0 container answers a `coordinator` link, declares and discharges local transactions, and holds
messages posted in a transaction until a commit publishes them all or none.

## Context

- ADR-0146 decisions 1.1 to 1.4 and 1.6 to 1.8; AMQP 1.0 Part 4 sections 4.2, 4.3, 4.4.1 and 4.5.1 to 4.5.8
  (descriptors `coordinator` 0x30, `declare` 0x31, `discharge` 0x32, `declared` 0x33,
  `transactional-state` 0x34).
- Ends ADR-0107 decision 4's refusal of a coordinator (`Amqp1:Surl-Nodes-6`,
  `Amqp1ContainerConnectionTests.ServeAsync_AttachToACoordinator_IsRefusedWithNotImplemented`).
- The commit uses BL-961's all-or-none batch publish.
- Matrix rows: `Amqp1:Transactions-4.5.1-1`, `-4.2-1`, `-4.2-2`, `-4.4.1-1`, `-4.4.1-2` (transfer, discharge),
  `-4.3-1`, `-4.3-2`, `-4.4-1`, `Amqp1:Surl-Transactions-1` in `Documentation/Conformance/Extended/Amqp1.md`.

## Acceptance criteria

- [ ] A coordinator attach is answered with a receiving link and a `coordinator` listing exactly
      `amqp:local-transactions`, `amqp:multi-txns-per-ssn` and `amqp:multi-ssns-per-txn`; `Amqp1:Surl-Nodes-6`
      is removed and its test replaced.
- [ ] `declare` answers `declared` with ids `00 00 00 01`, `00 00 00 02`, ...; a `global-id`, a 101st open
      transaction and any other body are rejected with the conditions ADR-0146 decision 1.3 names.
- [ ] A transactional transfer is held and answered `transactional-state`/`accepted`; a refused one
      `transactional-state`/`rejected`; an unknown `txn-id` `rejected` with `amqp:transaction:unknown-id`.
- [ ] `discharge` commits all or none (a test with the bus refusing the second message shows none published and
      `amqp:transaction:rollback`), rolls back with `fail` true, and answers an unknown id
      `amqp:transaction:unknown-id`.
- [ ] Coordinator detach, session end and connection end roll back an open transaction (a test each).
- [ ] Each row above names its tests in `Amqp1.md`; 100% line and branch coverage holds.

## Notes

- Filed by BL-707 (ADR-0146 decision 1).

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
