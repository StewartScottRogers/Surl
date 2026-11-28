---
id: BL-2042
title: Delete an AMQP 1.0 dynamic target queue when its sending link ends
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2042 — Delete an AMQP 1.0 dynamic target queue when its sending link ends

## Goal

No dynamic node's queue outlives the link whose attach made it.

## Context

- Found while working BL-1990 (2026-11-27) by reading the code; prove it with a failing test first.
- ADR-0107 decision 4, "Dynamic nodes": the queue is exclusive to the connection and auto-deleted when the link
  detaches, and a client sender's `dynamic` target is answered the same way.
- `Amqp1BusNodeDirectory.DeclareDynamicQueue` declares it `Exclusive` and `AutoDelete`. The bus auto-deletes only
  when a consumer ends (`Surl.MessageBus.UnitLibrary`, `MessageBus.Queues.cs`, `End`), and a client sender's link
  has no consumer, so its queue stays until the connection closes and counts against the bus's queue bound.
- The same holds for an attach refused after the queue was declared (a chosen-error fault on `attach`:
  `Amqp1ContainerConnection.Links.cs`, `AttachLinkAsync`).
- Links end through `EndLink` in that file (detach, session end, connection end, container detach).
  `BusConnection.DeleteQueue` needs no library change. A link that consumes a dynamic queue it did not make must
  not delete it by this rule.
- If the tests show no defect, ADR-0107's "Dynamic nodes" bullet needs a dated note instead: file that task.
- Stand-in: the shared message bus; the delete is published on the event stream as any queue delete. No new fault.

## Acceptance criteria

- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: after a client sender's link to a dynamic target detaches, and after its session ends, the bus has no such queue; the test failed before the change (or Notes say why this is no defect).
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: an attach with a dynamic source or target refused by a chosen-error fault leaves no queue.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: a link that attached to a dynamic queue by its address and ends without consuming from it leaves the queue while the link that made it is attached.
- [ ] The library's `CLAUDE.md` states when a dynamic queue is deleted, and row `Amqp1:Messaging-3.5.3-1` in `Amqp1.md` cites the new tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
