---
id: BL-2054
title: Align the other documents that describe direct sends with ADR-0146 Amendment 6
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md, Documentation/Planning/Decisions/ADR-0112-how-the-amqp-0-9-1-broker-answers-amqp-and-amqps.md, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2054 — Align the other documents that describe direct sends with ADR-0146 Amendment 6

## Goal

Every document and doc comment that describes a direct send to a queue says that a queue carrying a subscription
description takes none.

## Context

- Found while working BL-2012 (2026-11-27), which added ADR-0146 Amendment 6 and could edit only that ADR.
- `Surl.MessageBus.UnitLibrary/CLAUDE.md`, "Subscription queues", lists the default, direct, fanout and headers
  exchanges. `Amqp0BrokerConnection` sends every exchange but `amq.topic` through `PublishToQueue`, so
  client-declared topic exchanges belong in the list.
- `MessageBus.SeedResetSnapshot.cs`: `SeedEnqueue`'s comment says it puts a message "exactly as `PublishToQueue`",
  naming grants and the fault plan as the differences. It also fills a described queue.
- `MessageBus.Queues.cs`: `PublishToQueue`'s summary says "Any connection may send to any queue", and the
  `string? account` overload's returns text does not mention the described queue.
- ADR-0100's table of outcomes (the `UnknownQueue` row) and ADR-0112's (unroutable "on the default exchange") do
  not mention a described queue. Add a dated pointer to ADR-0146 Amendment 6 to each.
- ADR-0146 decision 3.4 says "so only the subscriber reaches it"; Amendments 2 and 5 let granted callers delete,
  purge, get and consume by name. Add a pointer there.

## Acceptance criteria

- [x] `Surl.MessageBus.UnitLibrary/CLAUDE.md` says a direct send through every AMQP 0-9-1 exchange but `amq.topic` is refused for a described queue.
- [x] The doc comments of `SeedEnqueue` and both public `PublishToQueue` overloads without `onlyWhenConsumed` state how each treats a described queue, and no comment says any connection may send to any queue without that exception.
- [x] ADR-0100 and ADR-0112 each carry a dated pointer to ADR-0146 Amendment 6 where they describe `UnknownQueue` for a direct send, and ADR-0146 decision 3.4 points to Amendments 2 and 5.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-28: Edited directly instead of delegating to `align-and-document`: the context named every place,
  so a second agent would only have re-read them. No behaviour changed.
- Also qualified two statements the context did not name: the library `CLAUDE.md`'s "may still send to it" for an
  exclusive queue, and ADR-0100 decision 3.3's "Any connection may send to it". A non-durable subscription queue is
  exclusive, so both needed the exception.
- ADR-0100 and ADR-0112 also gained an "Amended:" header line for Amendment 6, as their other amendments have.
- Verify: build 0 warnings, format clean, fast tests 30759 passed, 0 failed, 3 skipped.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. Every document and doc comment on direct sends now says a subscription queue takes none
