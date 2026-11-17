---
id: BL-1365
title: Cover the message bus queue-full plan and the topic name switch edges
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1365 — Cover the message bus queue-full plan and the topic name switch edges

## Goal

`Measure-CodeQuality.ps1 -Library Surl.MessageBus.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 100% line, 99.66% branch, 4 failing
  members, branch-only, in `Protocols/Messaging/Surl.MessageBus.UnitLibrary/`:
  - `MessageBus.CopyIntoBoundQueues(...)` - `MessageBus.Queues.cs:355`, 92.86% branch; line 365 is 5 of 6:
    `queuesFullByPlan?.Contains(queue) != true && Enqueue(...) is MessageBusOutcome.Succeeded` misses one outcome
    (a queue in the full-by-plan set, or `Enqueue` not succeeding).
  - `TopicNameLookup.DefaultToCanonicalTopic` (line 158), `DefaultToCanonicalFilter` (line 168) and
    `DefaultFromCanonicalTopic` (line 179), each 88.89% branch, 8 of 9. `SchemeFamily` is a private enum of eight
    members, so the ninth edge is most likely the compiled switch's out-of-range path, which no value can reach.

## Acceptance criteria

- [ ] A named `Surl.MessageBus.UnitTests` test publishes to a binding whose queue is in the full-by-plan set (and, if
      that side is already covered, one whose `Enqueue` refuses), asserting the queued and refused counts.
- [ ] For each of the three `TopicNameLookup` defaults: if a `SchemeFamily` member is the missing edge, a named test
      maps a name for it; if the missing edge is the out-of-range path, the method is reshaped (for example an explicit
      arm per family) so the compiled code has no unreachable edge, the behaviour of every family is unchanged, and the
      Notes say which it was.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.MessageBus.UnitLibrary` reports `100 | 100` with 0 failing members, and
      the fast tests are green.

## Notes

- Tests first (test-writer); production code changes only for an unreachable compiled edge, as BL-1055 did.

## Log

- 2026-11-17: Created.
