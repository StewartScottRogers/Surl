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
completed: 2026-11-11
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

- [x] A coordinator attach is answered with a receiving link and a `coordinator` listing exactly
      `amqp:local-transactions`, `amqp:multi-txns-per-ssn` and `amqp:multi-ssns-per-txn`; `Amqp1:Surl-Nodes-6`
      is removed and its test replaced.
- [x] `declare` answers `declared` with ids `00 00 00 01`, `00 00 00 02`, ...; a `global-id`, a 101st open
      transaction and any other body are rejected with the conditions ADR-0146 decision 1.3 names.
- [x] A transactional transfer is held and answered `transactional-state`/`accepted`; a refused one
      `transactional-state`/`rejected`; an unknown `txn-id` `rejected` with `amqp:transaction:unknown-id`.
- [x] `discharge` commits all or none (a test with the bus refusing the second message shows none published and
      `amqp:transaction:rollback`), rolls back with `fail` true, and answers an unknown id
      `amqp:transaction:unknown-id`.
- [x] Coordinator detach, session end and connection end roll back an open transaction (a test each).
- [x] Each row above names its tests in `Amqp1.md`; 100% line and branch coverage holds.

## Notes

- Filed by BL-707 (ADR-0146 decision 1).
- Delivered in the lane directly (one library and its tests, plus the matrix), not through the full
  `/feature` sub-agent pipeline: ADR-0146 already decides the behaviour.
- Design: `Amqp1ContainerConnection.Transactions.cs` keeps the connection's open transactions
  (`Amqp1Transaction`: number, declaring coordinator link, held `BusBatchEntry` list and bytes) and a
  counter for ids. `Amqp1TransactionControl` reads a coordinator's message (one `amqp-value` holding a
  `declare` or `discharge`; anything else, decode failures included, is `amqp:decode-error`). A finished
  delivery goes to the coordinator, into a transaction (state `transactional-state`), or to the bus as
  before. `StoreAsync` and the transactional hold share `ReadMessage` (format, decode, node entry).
  `EndLink` rolls back what a coordinator link declared, which covers detach, session end and
  connection end in one place.
- Choice: a coordinator as a *source* (client receiver) is refused with `amqp:not-allowed`
  ("a coordinator is only a target"); Part 4 defines it only as a target.
- Choice: the hold does not judge topic or queue access again. The attach already demands the publish
  right on the node (`Amqp1BusNodeDirectory`), grants never change during a run, and the commit's
  `PublishBatch` judges access again (a refusal there rolls back). A second check would be unreachable
  code under the 100% branch gate.
- Choice: a transfer's state is the last one any of its transfers carried, so a `transactional-state`
  on the first or last `more` frame both count.
- Choice: `--max-filesize` is `ExchangeContext.Limits.MaxUploadBytes` (what the attach answers as
  `max-message-size`); held bytes of every open transaction plus partial bytes plus the new message
  must fit.
- Choice: a commit the bus refuses notes `A transaction was rolled back: the bus refused its message
  <index> with <outcome>.` in the exchange log; the wire text stays fixed.
- ADR-0107's own text still describes the coordinator refusal; ADR-0146 records the amendment, and
  ADR-0107 is outside this task's `touches`, so it is left for whoever next edits it.
- Toolchain: the pinned SDK 10.0.401 is not installed on this machine (BL-1035). `global.json` was left
  unchanged; the build and tests were run with the installed SDK 11.0.100 from outside the repository
  (`cd /tmp; dotnet build Z:\...\Surl.slnx`), targeting net10.0: build clean (0 warnings, 0 errors),
  every fast test passed (Amqp1 730), Amqp1 library 100% line and branch, `dotnet format whitespace
  --folder` clean on both projects. CI on the pinned SDK is the final word.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. The AMQP 1.0 container serves a transaction coordinator: declare, transactional posting held until discharge, all-or-none commit through the bus batch, rollback on fail, detach, session end and connection end
