---
id: BL-1715
title: Decide whether an MQTT 3.1.1 subscription stays open for live publishes
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0014-how-the-mqtt-server-answers.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1715 — Decide whether an MQTT 3.1.1 subscription stays open for live publishes

## Goal

An ADR says whether `surl`'s MQTT 3.1.1 server keeps a `SUBSCRIBE` open and delivers later publishes, and files the build task if it does.

## Context

- Found in BL-1705: the server answers a `SUBSCRIBE` with retained messages, then `DISCONNECT` (ADR-0014, ADR-0176), so a
  subscriber never receives a later publish.
- Stand-in: a test could not start `curl mqtt://…/<topic>` and then publish over NATS or AMQP and have curl receive it.
- Measure pinned upstream curl 8.21.0 with `Record-CurlExchange.ps1`: what it does while a subscription stays open, with
  and without `--max-time`, and keep its plain fetch of a retained message working.

## Acceptance criteria

- [x] An ADR (or an ADR-0014 amendment) decides whether and when a subscription stays open, from measurements of pinned
      upstream curl, marked "Decided by Claude under Stewart's delegation".
- [x] It says how a test seeds, observes and ends such a subscription.
- [x] If the answer changes behaviour, the build task is filed in Backlog and named in the ADR.

## Notes

- Decided in ADR-0014 Amendment 1: off by default; `--mqtt-hold-subscription <count>` holds the subscription and sends
  `DISCONNECT` after `<count>` messages (0: no count). Keep Alive stays unenforced: pinned curl sends no `PINGREQ`
  while publishes arrive.
- Measured with `Record-CurlExchange.ps1 -Raw` (table in the amendment). A reply is sent only after curl pauses for
  `-RawIdleMilliseconds`, so holding a connection open is done with a short idle time and many empty replies; the
  script needed no change.
- Filed BL-1720 (server), BL-1721 (option, help, composition), BL-1722 (pinned curl `Integration` tests).
- Written in the lane's own session rather than by `align-and-document`: the work was the measurement and the
  decision. No `.cs` or project file changed, so no build or test run.
- Added `Documentation/Planning/Decisions/README.md` to `touches` for the ADR-0014 index row; no task in Doing names it.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. ADR-0014 Amendment 1 decides --mqtt-hold-subscription <count> from pinned curl 8.21.0 measurements; build filed as BL-1720 to BL-1722
