---
id: BL-691
title: Amend ADR-0100 decision 3.2 for NATS '>' as one-or-more levels and Kafka names with empty levels
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-630]
touches: [Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-691 — Amend ADR-0100 decision 3.2 for NATS '>' as one-or-more levels and Kafka names with empty levels

## Goal

ADR-0100 decision 3.2's mapping table and "What does not survive a mapping" say what BL-630's
`Surl.MessageBus` name mappings do, for NATS's `>` and for Kafka topic names with empty levels.

## Context

- BL-630 implemented the mappings in `Protocols/Messaging/Surl.MessageBus.UnitLibrary`
  (`NatsSubjectNames`, `KafkaTopicNames`). Two points read the ADR by each protocol's specification
  rather than its literal text; the ADR folder was held by BL-642 at the time, so the ADR could not
  be amended in BL-630.
- NATS: the ADR says "`>` (last only) becomes `#`". NATS's `>` matches one or more tokens (NATS
  client protocol, "Protocol conventions"), while canonical `#` matches zero or more levels, so
  `a.>` as `a/#` would wrongly receive a message on `a`. BL-630 maps the last `>` to `+/#` (one or
  more levels), and a trailing `+/#` back to `>`; a canonical `#` elsewhere is unreachable from NATS.
- Kafka: the ADR says Kafka "cannot name an empty level". Kafka's own rule,
  `[A-Za-z0-9._-]{1,249}` other than `.` and `..`, allows `a..b` and `.a`, and the ADR also says
  "the publisher is never refused for these: the topic is valid for its own protocol". BL-630 maps
  `a..b` to `a//b` and back; `/` and `//` (Kafka `.` and `..`) are unreachable.
- AMQP 0-9-1 keeps the ADR's literal rule: an empty word is refused (the spec's words are
  letters and digits).

## Acceptance criteria

- [ ] ADR-0100 decision 3.2's NATS row says the last `>` becomes `+/#`, and why, citing the NATS
      client protocol.
- [ ] ADR-0100 decision 3.2's Kafka row and "What does not survive a mapping" say Kafka names
      empty levels (`a..b` is `a//b`) and only `.` and `..` are refused.
- [ ] The amendment is marked "Decided by Claude under Stewart's delegation", dated, and names BL-630.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
