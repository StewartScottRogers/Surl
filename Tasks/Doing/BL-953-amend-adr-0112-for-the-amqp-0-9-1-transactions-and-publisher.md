---
id: BL-953
title: Amend ADR-0112 for the AMQP 0-9-1 transactions and publisher confirms BL-731 serves
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-731, BL-1035]
touches: [Documentation/Planning/Decisions/ADR-0112-how-the-amqp-0-9-1-broker-answers-amqp-and-amqps.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-953 — Amend ADR-0112 for the AMQP 0-9-1 transactions and publisher confirms BL-731 serves

## Goal

ADR-0112 states what the broker now does for `tx` and `confirm` (BL-731) instead of "540 until BL-731".

## Context

- BL-731 served the `tx` class and RabbitMQ's `confirm.select` in
  `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary` (`Amqp0BrokerConnection.Transactions.cs`,
  `Amqp0Transaction`; the `Amqp0Broker` remarks list every answer). It could not edit the ADR because
  `Documentation/Planning/Decisions` was in BL-701's `touches` while it ran.
- The behaviour to record, decided by Claude under Stewart's delegation in BL-731 (see its Notes):
  - A transacted channel buffers its whole publishes and its `ack`/`reject`/`nack` in order; `commit` routes and
    settles them (returns for `mandatory` are sent then), `rollback` or the channel's close discards them and
    the settled deliveries are outstanding again. A tag not outstanding is 406 at once. Buffered bodies count
    toward `--max-filesize` with the content in progress (506 above it). `select` twice is answered twice.
  - Confirm mode numbers publishes from 1 and answers each with its own `basic.ack`, `multiple` clear, because
    each publish is routed before the next frame is read, so there is never more than one to confirm. A publish
    the bus refuses for a bound (`StoreFull`, `TooManyTopics`, and a persistent message to a full queue - the
    rows that are 506 outside confirm mode) is `basic.nack`, `multiple` and `requeue` clear, and the channel
    stays open; 403 and 311 stay channel exceptions. An unroutable publish is acked (after its `basic.return`).
  - `tx.select` in confirm mode and `confirm.select` on a transacted channel are channel exception 406.
  - `publisher_confirms` is in `connection.start`'s capabilities.

## Acceptance criteria

- [x] ADR-0112 decision 6 replaces the "Transactions ... are BL-731's" bullet with the behaviour above, its
      `Succeeded` row no longer says publishes are unacknowledged outside a pending task, and decision 2's
      `server-properties` row lists `publisher_confirms` unconditionally.
- [x] ADR-0112 decision 7's `540` row no longer lists `tx` and `confirm`, and a `406` row (or its text) names
      `commit`/`rollback` on a channel not transacted and mixing `tx` with `confirm`.
- [x] The amendment is marked "Decided by Claude under Stewart's delegation" and dated.

## Notes

- Filed by BL-731.
- Done directly rather than through align-and-document: one file, wording given by the task; each claim checked against `Amqp0Broker`'s remarks and `Amqp0BrokerConnection.Transactions.cs` (406 for mixing tx and confirm, 406 for commit/rollback untransacted, `publisher_confirms` in `Amqp0BrokerConnection.Login.cs`). The header gains an "Amended" line for decisions 2, 6 and 7, and Consequences no longer says confirms wait for BL-731.
- Lane 3 (2026-11-11) cherry-picked lane 6's ADR change unchanged and checked it against the code; the boxes are ticked. It cannot reach Done because `dotnet build` fails: the pinned SDK 10.0.401 is not installed (BL-1035). Added BL-1035 to depends-on. Next run: `git cherry-pick --no-commit factory/BL-953-lane-6-20261111-060243`, keep only the ADR file, build, test, move to Done.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 6 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-953-lane-6-20261111-060243; start with git cherry-pick --no-commit factory/BL-953-lane-6-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: dotnet build fails because the pinned .NET SDK 10.0.401 is not installed; the ADR amendment itself is ready on factory/BL-953-lane-6-20261111-060243.
- 2026-11-12: Backlog -> Doing.
