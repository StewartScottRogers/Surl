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
completed: 2026-11-28
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

- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: when the link that made a dynamic node detaches, another link consuming its queue is sent a closing `detach` with `amqp:resource-deleted`.
- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: when the whole session or connection ends, no `detach` is written for the links ending with it.
- [x] The library's `CLAUDE.md` states it, and `Amqp1.md` cites the new tests.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Plan: `IAmqp1NodeDirectory.DeleteDynamicNode` answers whether it deleted the queue; the container keeps those
  addresses and, once the frame is answered and before the pump sends, detaches every link still consuming one
  (`DetachConsumersOfDeletedNodesAsync`). A link that ended with the node is no longer among them, and the
  connection's end does not run it.
- Choice (Claude): a link of **another** session consuming the queue is detached when the maker's session ends,
  because that link does not end with it.
- Choice (Claude): only consuming links are detached. A link sending to the queue by its address stays, as it
  waits for nothing; an unsettled transfer from it is `rejected` with `amqp:not-found` while no queue has the name.
- Choice (Claude): nothing is detached for a queue the container did not delete (already deleted by the bus, or a
  durable one declared anew under the name). The error `description` is the fixed text `the node was deleted`.
- Pipeline: planned, tested and implemented in the lane session, not by separate agents, to save tokens on a
  one-method change; `code-reviewer` reviewed it (two test gaps and two inaccurate statements, all fixed).
  No `conformance-auditor` run: upstream curl cannot speak `amqp1` (ADR-0083), and the specification rows are in
  `Amqp1.md` (`Amqp1:Messaging-3.5.3-3`).
- Gates: build 0 warnings, `dotnet format --verify-no-changes` clean, 30778 fast tests passed and 3 skipped,
  library 100% line and 100% branch coverage, 0 failing members.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. An AMQP 1.0 link left consuming a deleted dynamic queue is detached with amqp:resource-deleted
