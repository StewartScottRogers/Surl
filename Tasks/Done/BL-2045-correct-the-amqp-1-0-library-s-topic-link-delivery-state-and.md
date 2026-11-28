---
id: BL-2045
title: Correct the AMQP 1.0 library's topic link, delivery state and detach statements
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/CLAUDE.md, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1DeliveryState.cs, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1ContainerConnection.Links.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2045 — Correct the AMQP 1.0 library's topic link, delivery state and detach statements

## Goal

The AMQP 1.0 library's notes and comments say which statements hold only for live subscriptions, which delivery
states the library writes, and what outlives a detach.

## Context

Found while working BL-1990 (2026-11-27). All paths are in
`Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary`.

- `CLAUDE.md`, "Transfers, credit, settlement and the message format": "A client receiver's link is a bus
  subscription (topic) or a consumer", "a topic link drops past 1000 and notes the count when it ends" and "a
  topic delivery is only settled" hold for a live subscription only. A kept subscription consumes a bus queue
  (the "Durable and shared topic subscriptions" section): nothing is dropped at the link, and the client's outcome
  acts on the queue.
- `Amqp1DeliveryState.cs`, the class's XML summary names `accepted` and `rejected` as what the container sends. The
  class also writes `declared` and `transactional-state` (`Declared`, `TransactionalState`,
  `TransactionalAcquisition`).
- `Amqp1ContainerConnection.Links.cs`, in `AnswerDetachAsync`, the comment "Nothing of a link is kept for
  resumption" does not allow for a kept subscription, whose queue survives a detach with `closed` false (ADR-0146
  decisions 3.4 and 3.6). The link itself is never resumed.
- BL-2010, BL-2039, BL-2041, BL-2042 and BL-2043 change behaviour in this library: describe the code as it is when
  this task runs.
- Comments and notes only; no behaviour, store, event or fault changes.

## Acceptance criteria

- [x] The three `CLAUDE.md` statements in Context each say they hold for a live subscription, and what a kept subscription does instead.
- [x] `Amqp1DeliveryState`'s XML summary names every delivery state the class writes.
- [x] The `AnswerDetachAsync` comment says the link is never resumed and that a kept subscription's queue can outlive it.
- [x] The diff changes only comments in the two `.cs` files and text in `CLAUDE.md`.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Delivered by `align-and-document`; diff reviewed, then `verify`: build 0 warnings, format check clean, fast
  tests green (0 failed). No integration tests: only comments and notes changed.
- The `CLAUDE.md` statements name non-durable shared subscriptions beside kept ones, because their links consume
  a bus queue the same way (BL-2041).
- The `Amqp1DeliveryState` summary also names what the class reads, and drops "as a receiver": the container
  sends `rejected` as a sender too, for an unknown `txn-id`.
- Four other false or incomplete statements are outside this task's three; filed as BL-2084.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The AMQP 1.0 library's topic link, delivery state and detach statements match the code; follow-up statements filed as BL-2084
