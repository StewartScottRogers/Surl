---
id: BL-704
title: Record BL-664's bus configuration and access rules in ADR-0102
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-664]
touches: [Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md, Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-704 — Record BL-664's bus configuration and access rules in ADR-0102

## Goal

ADR-0102 decision 3 states the rules BL-664 settled while building `--topic`, `--queue`,
`--topic-map` and `--topic-access` into `Surl.MessageBus`, so the ADR is true of the code.

## Context

- BL-664 implemented ADR-0102 decision 3 in `Protocols/Messaging/Surl.MessageBus.UnitLibrary`
  and recorded its choices in that library's `CLAUDE.md` ("Configuration (BL-664)") and in
  BL-664's Notes. It could not edit the ADR: BL-487 held `Documentation/Planning/Decisions`.
- The choices to add, each marked "Decided by Claude under Stewart's delegation": the caller is
  an account name (`null` for anonymous) on `MessageBus.Connect(string?)`,
  `Publish(BusMessage, string?)` and `PublishToQueue(string, BusMessage, string?)`, and the
  parameterless forms are anonymous; access is scheme-independent, so the scheme reaches the bus
  only through `TopicNameLookup`; the outcome `MessageBusOutcome.AccessDenied`, also the answer
  to deleting a declared queue; a queue's name is one level of `$queue/<name>` whatever it holds;
  the schemes `TopicNameLookup` knows (Redis is left to BL-678); a STOMP or AMQP 1.0 alias names
  a `/topic/<t>` address; of two aliases with one target the first is sent back; a queue the
  files persisted as durable but now declared not durable is made again empty at load.

## Acceptance criteria

- [x] ADR-0102 decision 3 states each choice listed under Context, citing BL-664.
- [x] `Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md` no longer says these rules wait for BL-704.

## Notes

- Done in the session rather than through align-and-document: two Markdown edits, no `.cs` or project file. Added ADR-0102 decision 3.5 "What BL-664 settled in `Surl.MessageBus`", checked against the code (`TopicNameLookup` scheme table and alias `TryAdd`, `Connect(string?)`, `Publish(message)`); it also records that unbinding needs no grant and that a name mapped twice on one scheme is refused, both from BL-664. The library CLAUDE.md now points to decision 3.5 and lists `mqtt5+ws`, which `TopicNameLookup` knows. Build clean, fast tests green.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0102 decision 3.5 records BL-664's bus caller, access, alias and declared-queue rules
