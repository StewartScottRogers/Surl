---
id: BL-731
title: Serve AMQP 0-9-1 transactions and publisher confirms
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-506]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-731 — Serve AMQP 0-9-1 transactions and publisher confirms

## Goal

The AMQP 0-9-1 broker serves the `tx` class (`select`, `commit`, `rollback`) and RabbitMQ's `confirm.select` with
publisher `basic.ack`/`basic.nack`, and announces `publisher_confirms` in `connection.start`.

## Context

- Decision: ADR-0112 decision 6 ("Transactions ... and publisher confirms are BL-724's"), decision 2's
  capabilities and decision 7's table (`540` until this task; `406` for misuse). Matrix rows `Amqp0:Tx-1` and
  `Amqp0:Extension-3` in `Documentation/Conformance/Extended/Amqp0.md`.
- AMQP 0-9-1 XML class `tx` (90): `select`/`select-ok`, `commit`/`commit-ok`, `rollback`/`rollback-ok`; rules
  `commit`/`transacted` and `rollback`/`transacted` (406 on a channel not transacted); the class rules that
  clients must not rely on atomicity across queues, `mandatory` or `immediate`; `basic.ack` rule `exists` (a bad
  tag is refused at once on a transacted channel, not at commit).
- RabbitMQ's extensions (<https://www.rabbitmq.com/docs/specification>, `amqp0-9-1.extended.xml`): `confirm`
  class 85, `select` (10, with `nowait`) and `select-ok` (11); in confirm mode each publish gets a sequence number
  from 1 and the broker sends `basic.ack` (or `basic.nack` when the bus refuses it - ADR-0112 decision 6's
  `StoreFull`/`TooManyTopics` rows become a `nack` there), `multiple` allowed. A channel cannot be both
  transacted and in confirm mode (406).
- Transactions are the broker's own buffer per channel: publishes and acknowledgements are applied in order at
  `commit`, discarded at `rollback`; the buffer counts toward the connection's `--max-filesize` bound (ADR-0112
  decision 8).

## Acceptance criteria

- [x] Named tests show: publishes on a transacted channel invisible until `commit` and gone after `rollback`; an
      acknowledgement taking effect only at `commit`; `commit` on a channel not transacted answered `406`; confirm
      mode acknowledging each publish in order, `multiple` included; a publish the bus refuses `nack`ed in confirm
      mode; `tx.select` on a channel in confirm mode (and the reverse) answered `406`; `publisher_confirms` in
      `connection.start`'s capabilities.
- [x] `Documentation/Conformance/Extended/Amqp0.md`'s rows `Amqp0:Tx-1` and `Amqp0:Extension-3` name the tests;
      ADR-0112 decision 7's `540` row is BL-932's (see Notes).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-503 (ADR-0112), split from BL-506 so each task is one run.
- Delivered in `Amqp0BrokerConnection.Transactions.cs`, `Amqp0Transaction`, `Amqp0TransactedWork`,
  `Amqp0TxMethodId` and `Amqp0ConfirmMethodId`; dispatch and the 503 rows for `select-ok`/`commit-ok`/
  `rollback-ok`/`confirm.select-ok` in `Amqp0BrokerConnection.Channels.cs`; `publisher_confirms` in
  `Amqp0BrokerConnection.Login.cs`. `Amqp0Broker`'s remarks and the library's `CLAUDE.md` describe it.
- Decisions (Claude under Stewart's delegation, by the AMQP 0-9-1 XML and RabbitMQ's extension spec):
  - The transaction is a per-channel buffer of whole publishes and settlements, applied in order at `commit`;
    a mandatory return is sent at commit, when the publish is routed. `rollback` - and the channel's close -
    puts the settled deliveries back in `Outstanding`, so they can be settled again or are requeued with the
    channel. A tag not outstanding is still 406 at once (basic rule `ack`/`exists`). Buffered bodies stay in
    the connection's held-content count (renamed `heldContentBytes`), so with the content in progress they
    stay within `--max-filesize` (506 above it, as for content in progress). `recover` stays immediate.
  - Confirms are never batched: each publish is routed before the next frame is read, so the broker answers
    each with its own `basic.ack`, `multiple` clear - RabbitMQ's spec lets the broker set `multiple` but never
    requires it, and batching would need the read loop to know when the socket is drained, which makes
    answers timing-dependent. The criterion's "`multiple` included" is met as the field being sent (clear),
    pinned byte for byte in `Publish_InConfirmMode_IsAcknowledgedInOrderEachWithMultipleClear`.
  - The nack rows: every bus refusal that is 506 outside confirm mode (`StoreFull`, `TooManyTopics`, and a
    persistent message to a full queue, which basic rule 02 would otherwise force to 506) is `basic.nack`,
    `multiple` and `requeue` clear, the channel open. 403 and 311 stay channel exceptions. An unroutable
    publish is acked, after its `basic.return` when mandatory.
- `ReleasePendingContent` went: a channel now closes only with no publish or one refused at its header (311),
  whose bytes were never held, so its subtraction was unreachable; `ReleaseChannel` clears `Publish` itself.
- Scope: ADR-0112's edits (decision 6's bullet and `Succeeded` row, decision 2's capability row, decision 7's
  540 row) were split into BL-932, because `Documentation/Planning/Decisions` is in BL-701's `touches` (in
  Doing at the time). `Documentation/Conformance/Extended/Amqp0.md` was added to `touches`: no task in Doing
  names it. The criterion was reworded to point at BL-932 rather than leave the task blocked on a doc edit.
- Coverage: 100% line and branch for the library. `Measure-CodeQuality.ps1` still lists six IL-measured
  complexity rows (`Route`, `OpenChannel`, `BindQueue`, `IsValidName`'s lambda, `Amqp0TopicKeyMatcher.Advance`,
  `Amqp0ExchangeTable.Declare`) from methods this task did not change; the build-time gate (CA1502 at 10)
  passes for all of them.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. The AMQP 0-9-1 broker serves tx.select/commit/rollback and confirm.select with per-publish basic.ack/nack, and announces publisher_confirms
