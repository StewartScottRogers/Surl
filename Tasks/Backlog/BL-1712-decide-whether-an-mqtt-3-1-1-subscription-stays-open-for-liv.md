---
id: BL-1712
title: Decide whether an MQTT 3.1.1 subscription stays open for live publishes
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0014-how-the-mqtt-server-answers.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1712 — Decide whether an MQTT 3.1.1 subscription stays open for live publishes

## Goal

An ADR says whether `surl`'s MQTT 3.1.1 server keeps a `SUBSCRIBE` open and delivers later publishes, and files the build task if it does.

## Context

- Found in BL-1705: the server answers a `SUBSCRIBE` with retained messages, then `DISCONNECT` (ADR-0014, ADR-0176), so a
  subscriber never receives a later publish.
- Stand-in: a test could not start `curl mqtt://…/<topic>` and then publish over NATS or AMQP and have curl receive it.
- Measure pinned upstream curl 8.21.0 with `Record-CurlExchange.ps1`: what it does while a subscription stays open, with
  and without `--max-time`, and keep its plain fetch of a retained message working.

## Acceptance criteria

- [ ] An ADR (or an ADR-0014 amendment) decides whether and when a subscription stays open, from measurements of pinned
      upstream curl, marked "Decided by Claude under Stewart's delegation".
- [ ] It says how a test seeds, observes and ends such a subscription.
- [ ] If the answer changes behaviour, the build task is filed in Backlog and named in the ADR.

## Notes

## Log

- 2026-11-25: Created.
