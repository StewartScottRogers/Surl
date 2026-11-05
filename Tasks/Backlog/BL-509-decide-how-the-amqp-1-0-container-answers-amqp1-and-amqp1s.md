---
id: BL-509
title: Decide how the AMQP 1.0 container answers amqp1 and amqp1s
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-509 — Decide how the AMQP 1.0 container answers amqp1 and amqp1s

## Goal

An ADR decides how `surl amqp1://` and `amqp1s://` answer as an AMQP 1.0 container. It covers protocol-header
negotiation, the SASL mechanisms, the connection, session and link limits, the nodes (addresses) served, link
credit, settlement, the delivery states, storage and limits. A server-side requirement matrix traces each
decision.

## Context

- Oracle and rules: BL-543's ADR. Specification: OASIS AMQP Version 1.0 (29 October 2012,
  https://docs.oasis-open.org/amqp/core/v1.0/os/amqp-core-overview-v1.0-os.html):
  - Part 1 (types and encodings);
  - Part 2 (transport: headers, frames, open, begin, attach, flow, transfer, disposition, detach, end, close,
    and the error conditions);
  - Part 3 (messaging: message format, delivery states, source and target);
  - Part 5 (security: the TLS and SASL layers).
- Which frames a client sends (what to accept, not a verdict): the port's `Curl.Extended.Protocol.Amqp1.UnitLibrary`
  and its matrices `Amqp1.md` and `Amqp1-Messaging.md`. The port's `Amqp1ReferenceContainer` is copyable code.

Decide and record:

1. Header negotiation: SASL (`AMQP 3 1 0 0`) required by default; plain AMQP (`AMQP 0 1 0 0`) only under
   `--allow-anonymous`; and the TLS header (`AMQP 2 1 0 0`) answered or refused.
2. SASL: `PLAIN` against the accounts file, and `ANONYMOUS` only under `--allow-anonymous`, with the
   `sasl-outcome` codes.
3. The `open` fields sent (`max-frame-size`, `channel-max`, `idle-time-out`) and idle timeout on an injected
   `TimeProvider`.
4. Nodes: queues created on first attach, or only configured ones, and the error for an unknown address
   (`amqp:not-found`).
5. Link credit and the receiver's settlement modes, and the delivery states the container uses (accepted,
   rejected, released, modified).
6. Multi-frame transfers, the largest message, and storage across restarts.
7. `amqp1s` over TLS, and each ADR-0006 limit.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing its Part and section, and is
      listed in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/Amqp1.md` exists in the matrix format, each "covered by" naming
      BL-510, BL-511, BL-512 or BL-513, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
