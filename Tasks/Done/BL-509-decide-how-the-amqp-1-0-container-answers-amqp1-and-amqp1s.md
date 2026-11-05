---
id: BL-509
title: Decide how the AMQP 1.0 container answers amqp1 and amqp1s
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401, BL-622]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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
- Shared message bus (Stewart, 2026-11-04): BL-622's ADR decides one topic space shared by every messaging
  server in one `surl`, held by the horizontal library it names (planning `Surl.MessageBus`), and how an AMQP 1.0
  link address maps onto its topics and queues. Decisions 4 and 6 below apply that mapping (nodes are the bus's
  topics and queues, storage across restarts is the bus's) and add only what is AMQP 1.0-specific. Logins read
  the one account set (ADR-0032; BL-622 decision 4).

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

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing its Part and section, and is
      listed in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/Amqp1.md` exists in the matrix format, each "covered by" naming
      BL-510, BL-511, BL-512 or BL-513, or "n/a: <reason>".

## Notes

- ADR-0107 decides 1 to 7; matrix `Documentation/Conformance/Extended/Amqp1.md` (rows to BL-510 to BL-513, three
  n/a). Written directly in this session rather than through `align-and-document`: the task is a decision record,
  and the decisions needed the bus code (`Amqp1AddressNames`, `BusQueueConsumer`) read alongside the specification.
- Learned from the port's client (what a client sends, not a verdict): it attaches to the URL path **without its
  leading `/`**, so `amqp1://host/orders` is address `orders`. ADR-0100's row refused any address without
  `/topic/` or `/queue/`; ADR-0107 decision 4 amends it: one leading `/` is dropped and a bare name is a queue.
  ADR-0100 gained an "Amended" line. `Surl.MessageBus` code is unchanged (the container rewrites the address).
- Default taken: queues are created durable on first attach (sender or receiver), bounded by the bus's
  `MaxQueues`, rather than only `--queue` ones - the anonymous-safe, AMQP-0-9-1-like choice.
- Default taken: the TLS header (`AMQP 2 1 0 0`) is upgraded in band on `amqp1` when a certificate is configured,
  through the existing `IConnection.UpgradeToTlsAsync`; otherwise answered with the SASL header and closed.
- Filed BL-704 (bus carries a publisher's native encoding, so the AMQP sections reach an `amqp1` receiver byte
  for byte, as BL-513's criteria require) and added it to BL-513's `depends-on`. Filed BL-705 (decide
  transactions, durable topic subscriptions and source filters), which ADR-0107 marks "Not served yet".

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-622; Context says the message bus is shared.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0107 decides how the AMQP 1.0 container answers amqp1 and amqp1s; matrix Amqp1.md traces it to BL-510 to BL-513; BL-704 and BL-705 filed
