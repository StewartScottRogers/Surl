---
id: BL-1605
title: Decide whether seeding a message into a queue nobody declared creates the queue or is refused
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md, Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md]
requirement: none
created: 2026-11-22
completed:
---
# BL-1605 — Decide whether seeding a message into a queue nobody declared creates the queue or is refused

## Goal

ADR-0124 decision 3 gains an amendment, decided by Claude under Stewart's delegation, that states with its reason
whether a test's seed into a queue that does not exist (`Surl.Api` `Messages.EnqueueAsync`, `surl-mcp`
`publish_message` with `queue`, control `POST /v1/messages` with `queue`) creates the queue or is refused
`UnknownQueue`, and why that differs, or not, from a STOMP `SEND` or an AMQP 1.0 attach, which create it.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T1308-Surl-bug-report.md` item 3 (question):
  with `surl --queue jobs stomp://127.0.0.1:61613/`, a STOMP `SEND destination:/queue/other` is accepted and creates
  `other`, while `EnqueueAsync("other", ...)` is refused. Recipe `amqp-check-what-a-queue-holds-in-process`, case
  `wrong-queue`. (Tested with the Curl port as client; a symptom report, not evidence.)
- What is decided, with a reason:
  - **STOMP creates**: ADR-0118, destinations: "A queue that does not exist is created by the first SEND or SUBSCRIBE
    naming it, with ADR-0102 decision 3.2's flags for a declared queue (durable, never exclusive, never auto-deleted),
    as ADR-0107 does for AMQP 1.0, so a SEND before any subscriber is kept and a STOMP producer meets an AMQP consumer
    on the same queue." Code: `Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary/StompServerConnection.Routing.cs:120`
    and `:134` (`OnBus.DeclareQueue(queue, DeclaredQueueFlags)` before the publish).
  - **AMQP 1.0 creates**: ADR-0107, "Nodes are created on first attach", called there the anonymous-safe default.
  - **AMQP 0-9-1 does not**: ADR-0100's `UnknownQueue` row: a publish to the default exchange naming no queue is
    unroutable (ADR-0112).
  - **The bus refuses**: ADR-0100 decision 3 ("a queue that does not exist is `UnknownQueue`").
- What is not decided with a reason: ADR-0124 decision 3 says only that a seed "writes through the same path a
  protocol uses - ... publish or enqueue for the bus". The protocols' paths disagree, and no ADR says which one a seed
  follows or why. The code follows the bus primitive:
  `Protocols/Messaging/Surl.MessageBus.UnitLibrary/MessageBus.SeedResetSnapshot.cs:63` returns `UnknownQueue`, reached
  from `Application/Surl.Composition.UnitLibrary/ComposedStores.cs:377` (`SeedEnqueueAsync`),
  `Application/Surl.Api/SurlServerMessages.cs:55` (documented "which must already exist"),
  `Application/Surl.Mcp.UnitLibrary/PublishMessageTool.cs:60` and
  `Application/Surl.Composition.UnitLibrary/ComposedControlTarget.cs:121`.
- Weigh: a seed refusing an unknown queue catches a test's typo (the reasoning ADR-0132 amendment 1 and ADR-0102's
  BL-1483 amendment use); creating it matches the two brokers that create on demand and lets a test seed before any
  client attaches. If it creates, which flags (ADR-0102 decision 3.2's declared-queue flags, as STOMP uses), and whether
  reset removes a seeded-into-existence queue (ADR-0124 decision 3: reset returns to the configured queues).
  Configuration only grows and defaults keep today's behaviour (root `CLAUDE.md`). Upstream curl has no say: no curl
  scheme reaches the bus's queues.
- Stand-in: the shared message bus (ADR-0100 decision 3); a seed publishes `StoreChangedEvent` `Seeded`, and a
  created queue would need its own event or none, which the amendment states.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md` has an amendment dated 2026-11-22
      (BL-1605), marked "Decided by Claude under Stewart's delegation", stating the rule for a seed into an undeclared
      queue on all three hosts, the options weighed and why the chosen one, and why a STOMP SEND and an AMQP 1.0 attach
      still create the queue; its header's "Amended by" list names it.
- [ ] The amendment gives the exact answer on each host (`SurlSeedRefusedException` `UnknownQueue`, the `surl-mcp` tool
      error text, the control endpoint status and message) or, if seeding creates, the flags, the event published and
      what reset does with the queue.
- [ ] ADR-0100's `UnknownQueue` row or decision 3 text gains a pointer to the amendment where it describes seeds.
- [ ] If the rule changes behaviour, the implementation is filed as new Backlog tasks (one per project and its
      `.UnitTests` twin, each depending on BL-1605) and listed in this task's Log; if it keeps today's behaviour, the Log
      says no code change is needed.
- [ ] No `.cs` file changes.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
