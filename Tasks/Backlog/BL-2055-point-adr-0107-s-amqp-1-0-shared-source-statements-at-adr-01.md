---
id: BL-2055
title: Point ADR-0107's AMQP 1.0 shared-source statements at ADR-0146 Amendment 4
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2055 — Point ADR-0107's AMQP 1.0 shared-source statements at ADR-0146 Amendment 4

## Goal

ADR-0107's notes say that a shared AMQP 1.0 topic source asking no kept subscription consumes an auto-delete
subscription queue (ADR-0146 Amendment 4), as the code does since BL-2041.

## Context

- Found by the code review of BL-2041 (2026-11-27). BL-2036 held the file in `Doing` then.
- The note under decision 0 says "A `shared` source with `durable` `none` or `link-detach` makes a live
  subscription, which ends with its link." That is no longer so.
- The dated note under decision 5's topic delivery bullet ("hold for a live subscription only. A link on a kept
  subscription ... consumes the subscription's bus queue") says nothing of the non-durable shared queue, whose
  links get queue deliveries the same way.
- Amend only by adding a dated amendment note; do not rewrite the earlier text.

## Acceptance criteria

- [ ] A dated note beside the "makes a live subscription" statement says Amendment 4 replaced it, and what such a source is now.
- [ ] A dated note says the queue-delivery behaviour stated for kept subscriptions also holds for a non-durable shared one.

## Notes

## Log

- 2026-11-27: Created.
