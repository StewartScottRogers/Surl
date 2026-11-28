---
id: BL-2084
title: Say that a restore can leave a non-durable shared subscription queue after its last link
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2051]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary]
requirement: none
created: 2026-11-28
completed:
---
# BL-2084 — Say that a restore can leave a non-durable shared subscription queue after its last link

## Goal

The AMQP 1.0 library's comments say when a non-durable shared subscription queue outlives its last link.

## Context

- Found by the code review of BL-2051 (2026-11-28). ADR-0146 Amendment 8: a restore that changes a queue's
  description ends its consumers and keeps the queue, so a link ended that way no longer takes an auto-delete
  queue with it.
- `Amqp1SubscriptionQueue.cs` and `Amqp1ContainerConnection.Subscriptions.cs` in
  `Surl.Extended.Protocol.Amqp1.UnitLibrary` say the bus removes such a queue with its last link; check the
  library's `CLAUDE.md` for the same statement. Comments and documents only, no behaviour change.

## Acceptance criteria

- [ ] No comment or document in `Surl.Extended.Protocol.Amqp1.UnitLibrary` says without exception that the bus removes a non-durable shared subscription queue with its last link; each names the restore of ADR-0146 Amendment 8.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-28: Created.
