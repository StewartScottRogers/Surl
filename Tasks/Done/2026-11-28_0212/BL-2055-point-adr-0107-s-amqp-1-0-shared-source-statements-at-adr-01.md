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
completed: 2026-11-28
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

- [x] A dated note beside the "makes a live subscription" statement says Amendment 4 replaced it, and what such a source is now.
- [x] A dated note says the queue-delivery behaviour stated for kept subscriptions also holds for a non-durable shared one.

## Notes

- Added three dated (2026-11-28) amendments to ADR-0107: one under decision 0 after the BL-1990
  note, one under decision 5's "From a topic" bullet, and a header "Amended" line naming both. No
  earlier text was rewritten.
- Checked the notes against `Amqp1SubscriptionQueue.cs` (`IsAskedFor`, `QueueFlags`,
  `AsksToBeKept`), `Amqp1ContainerConnection.Subscriptions.cs`'s consume call and the library's
  `CLAUDE.md`.
- The edit was made in the lane session rather than delegated to `align-and-document`: two short
  notes in one file.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: every test project passed. No `.cs` or project
  file changed, so `/verify` was not needed.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. ADR-0107 now says a non-durable shared AMQP 1.0 topic source is an auto-delete subscription queue with queue deliveries (ADR-0146 Amendment 4)
