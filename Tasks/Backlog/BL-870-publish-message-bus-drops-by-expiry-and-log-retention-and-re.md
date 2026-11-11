---
id: BL-870
title: Publish message bus drops by expiry and log retention and refuse a bound queue's copy by fault
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-790, BL-1035]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-870 — Publish message bus drops by expiry and log retention and refuse a bound queue's copy by fault

## Goal

The message bus also publishes the changes BL-790 left unpublished - a queue message dropped
because it expired, and topic log records dropped by `MaxLogBytesPerTopic` retention - and a topic
publish's copy into a bound queue is refused as `QueueFull` (counted in
`BusPublishResult.FullQueueCount`) when an out-of-space rule names that queue's
`MessageBus.QueueChangeName`, as a full queue already refuses only its own copy.

## Context

- BL-790 publishes published, retained written or cleared (including a retained message dropped on
  expiry), delivered, enqueued, acknowledged, rejected-and-dropped, purged, queue declared and
  deleted, and log created; it consults the plan for a topic publish (by topic) and a direct
  `PublishToQueue` (by `$queue/<name>`) only.
- The copies into bound queues are made under the bus's lock, where the plan (which may run a
  `Surl.Api` hook) must not be called: ask for each bound queue before taking the lock, as
  `PublishToQueue` does.
- ADR-0124 decisions 1 and 4; `Protocols/Messaging/Surl.MessageBus.UnitLibrary/MessageBus.Events.cs`.

## Acceptance criteria

- [ ] Named `MessageBusEventTests` show a `Deleted` event, named `$queue/<name>` with the payload
      length, for each expired queue message dropped, and one for log records dropped by retention.
- [ ] A named `MessageBusOutOfSpaceTests` test shows a rule `path=$queue/q` refuses only queue `q`'s
      copy of a topic publish, `FullQueueCount` 1, while subscriptions and other bound queues get it.
- [ ] Every existing `Surl.MessageBus.UnitTests` test passes unchanged, the solution builds, the fast
      tests are green, and the library keeps 100% line and branch coverage.

## Notes

Filed by BL-790's run (dark factory lane 1).

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build fails
