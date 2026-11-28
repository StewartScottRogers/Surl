---
id: BL-2061
title: Detach AMQP 1.0 links left on a deleted dynamic queue with resource-deleted
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2042]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2061 — Detach AMQP 1.0 links left on a deleted dynamic queue with resource-deleted

## Goal

A client learns that a link's dynamic queue was deleted under it, instead of waiting on a link that gets nothing more.

## Context

- Found while working BL-2042 (2026-11-28). The container deletes a dynamic queue when the link that made it ends,
  even while another link of the connection consumes it. That link's consumer is deactivated by the bus, but the
  client is sent no `detach`.
- AMQP 1.0 Part 2 section 2.8.15 names `amqp:resource-deleted` (`Amqp1ErrorCondition.ResourceDeleted`, unused).
- Only the connection's own links can consume a dynamic queue (it is exclusive), so `DeleteMadeDynamicNode`
  (`Amqp1ContainerConnection.Links.cs`) has what it needs; no bus change. `DetachWithErrorAsync` is the container's
  detach. A queue another protocol deletes under a link is not this task.
- Stand-in: a test waiting on a reply queue fails at once with a named error. No new fault.

## Acceptance criteria

- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: when the link that made a dynamic node detaches, another link consuming its queue is sent a closing `detach` with `amqp:resource-deleted`.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: when the whole session or connection ends, no `detach` is written for the links ending with it.
- [ ] The library's `CLAUDE.md` states it, and `Amqp1.md` cites the new tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-28: Created.
