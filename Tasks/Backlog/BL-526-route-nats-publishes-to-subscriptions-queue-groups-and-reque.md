---
id: BL-526
title: Route NATS publishes to subscriptions, queue groups and requests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-525]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-526 — Route NATS publishes to subscriptions, queue groups and requests

## Goal

The NATS server routes PUB and HPUB to matching subscriptions as MSG and HMSG, with wildcards, queue groups (one
member per message), `UNSUB` limits, `echo` and no-responders. It cuts off slow consumers as BL-524's ADR
says.

## Context

- Decision: BL-524's ADR, decisions 3, 4 and 7.
- The protocol page: PUB, HPUB (header and total sizes), SUB with an optional queue group, UNSUB with
  `max_msgs`, MSG and HMSG (with `reply-to`), and the subject rules (tokens separated by `.`, `*` one token, `>`
  the rest). The headers ADR: `NATS/1.0` and `NATS/1.0 503` for no responders. `-ERR 'Maximum Payload Violation'`
  for a payload over `max_payload`, and `-ERR 'Invalid Subject'`.
- Subscriptions are shared by connections and owned by the server instance, not a static.

## Acceptance criteria

- [ ] Named tests show:
      - a PUB delivered as MSG to two subscribers;
      - `foo.*` and `foo.>` matching as the page says;
      - a queue group of two members getting one copy each of two messages between them;
      - `UNSUB 1 2` ending after two messages;
      - `echo: false` suppressing self-delivery;
      - HPUB delivered as HMSG with its headers byte for byte;
      - a request to a subject with no subscribers answered with a `503` HMSG when `no_responders` is set;
      - a payload over `max_payload` refused;
      - a slow consumer disconnected as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
