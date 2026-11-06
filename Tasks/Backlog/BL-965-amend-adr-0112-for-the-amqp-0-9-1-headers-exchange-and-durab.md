---
id: BL-965
title: Amend ADR-0112 for the AMQP 0-9-1 headers exchange and durable exchanges BL-732 serves
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-732]
touches: [Documentation/Planning/Decisions/ADR-0112-how-the-amqp-0-9-1-broker-answers-amqp-and-amqps.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-965 — Amend ADR-0112 for the AMQP 0-9-1 headers exchange and durable exchanges BL-732 serves

## Goal

ADR-0112 states what the broker now does for the headers exchange and durable exchanges (BL-732) instead of
"BL-732" and "until BL-732".

## Context

- BL-732 served them in `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary`
  (`Amqp0HeadersMatcher`, `Amqp0Binding`, `Amqp0ExchangeFiles`, `Amqp0ExchangeTable`; the `Amqp0Broker`
  remarks and the library's `CLAUDE.md` list every answer, and BL-732's Notes record the decisions). It could
  not edit the ADR because `Documentation/Planning/Decisions` was in BL-707's `touches` while it ran.
- The sentences to change: decision 5's exchange table (`amq.headers`, `amq.match` row "BL-732"; declared
  exchanges "headers with BL-732"), the `exchange.declare` bullets ("and `headers` once BL-732 serves it";
  "until BL-732 a durable exchange lives for the server's life and is not persisted"), decision 6's storage
  paragraph ("persist with BL-732; until then they last the server's life"), and the Consequences line
  "the headers exchange and durable exchanges across restarts for BL-732".
- What to state: the headers match (`x-match` `all` default or `any`, `x-` entries not matched, a void entry
  matching presence, value equality by type octet and bytes, `all` with no entry matching every message and
  `any` none, another `x-match` 406 on `queue.bind`; a headers binding is told apart by its arguments); the
  file `.surl/amqp0/exchanges` and its bytes (`Amqp0ExchangeFiles` remarks); saved after each channel frame
  that changed it, a failed write noted; malformed refused as `Amqp0ExchangeLoadException`, exit 37 by the
  composition (BL-966).

## Acceptance criteria

- [ ] ADR-0112 no longer says the headers exchange or durable exchanges wait for BL-732, and states each
      behaviour listed under Context, marked as an amendment decided by Claude under Stewart's delegation.

## Notes

## Log

- 2026-11-06: Created.
