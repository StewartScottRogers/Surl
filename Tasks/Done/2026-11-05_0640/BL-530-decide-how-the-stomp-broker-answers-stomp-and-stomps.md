---
id: BL-530
title: Decide how the STOMP broker answers stomp and stomps
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401, BL-622]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Stomp.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-530 — Decide how the STOMP broker answers stomp and stomps

## Goal

An ADR decides how `surl stomp://` and `stomps://` answer as a STOMP 1.2 broker. It covers version negotiation,
virtual hosts, logins, destination semantics, acknowledgement modes, receipts, transactions, heart-beating, ERROR
frames, TLS and limits. A server-side requirement matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specification: STOMP 1.2 (https://stomp.github.io/stomp-specification-1.2.html):
  frames and their augmented BNF, value encoding, repeated headers, size limits, CONNECT/STOMP and CONNECTED,
  heart-beating, the client frames (SEND, SUBSCRIBE, UNSUBSCRIBE, ACK, NACK, BEGIN, COMMIT, ABORT, DISCONNECT),
  the server frames (MESSAGE, RECEIPT, ERROR), and protocol negotiation.
- Which frames a client sends (what to accept, not a verdict): the port's `Curl.Extended.Protocol.Stomp.UnitLibrary`
  (the port's ADR-1059) and its matrix `Stomp.md`.
- ADR-0006 section 3: the CONNECTED `server` header names `surl` with no version.
- Shared message bus (Stewart, 2026-11-04): BL-622's ADR decides one topic space shared by every messaging
  server in one `surl`, held by the horizontal library it names (planning `Surl.MessageBus`), and how STOMP
  `/topic/…` and `/queue/…` destinations map onto its topics and queues. Decisions 4 and 5 below apply that
  mapping (kept messages and redelivery are the bus's) and add only what is STOMP-specific; a message published
  over `mqtt` must reach a STOMP subscriber. `login`/`passcode` read the one account set (ADR-0032; BL-622
  decision 4).

Decide and record:

1. Versions accepted (1.0, 1.1, 1.2), and the ERROR for no common version.
2. The `host` header: one virtual host or any.
3. `login`/`passcode` from the accounts file, required by default, with anonymous only under `--allow-anonymous`.
4. Destinations: the specification leaves them opaque. Decide the semantics (planning: `/queue/…` delivers each
   message to one subscriber, `/topic/…` to all), and whether unconsumed messages are kept.
5. Ack modes `auto`, `client` and `client-individual`, NACK, and redelivery.
6. Transactions, receipts, and DISCONNECT with a receipt.
7. Heart-beating: the server's offer and the tolerance before closing, on an injected `TimeProvider`.
8. `stomps` over TLS, and each ADR-0006 limit (frame size, header count and length, body size).

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 8, each citing the specification section, and
      is listed in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/Stomp.md` exists in the matrix format, each "covered by" naming
      BL-531 or BL-532, or "n/a: <reason>".

## Notes

- ADR-0118 decides it (the next free number on 2026-11-05). Summary: versions 1.0, 1.1 and 1.2 all
  served (a complete mate refuses no conforming client; the differences fit one table); one virtual
  host; logins through `IAuthenticationPolicy`; `/topic/` live fan-out and `/queue/` bus queues created
  durable on first use (as ADR-0107 does for AMQP 1.0); cumulative NACK in `client` mode; unknown ACKs
  ignored; every ERROR closes, which amends ADR-0100 3.6's STOMP `TooManySubscriptions` cell because
  the specification requires the close; heart-beat offer `1000,<quarter of --idle-timeout>` with a 2x
  tolerance; 128 headers a frame as a fixed bound; no new option.
- The port's client was read only for what it sends (`accept-version:1.2`, `host`, `heart-beat:0,0`,
  SEND to the URL path with its leading `/`, SUBSCRIBE `ack:auto`, DISCONNECT with `receipt`). So
  `stomp://host/queue/x` and `stomp://host/topic/x` reach the bus, and a bare path gets
  `Unknown destination`.
- The two `stomps` TLS rows in the matrix are n/a: `ImplicitTlsSchemeServer` wraps the broker and
  BL-533 composes and tests it. The acceptance criterion allows only BL-531, BL-532 or n/a.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-622; Context says the message bus is shared.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0118 decides how the STOMP broker answers stomp and stomps; Stomp.md traces each server-side requirement to BL-531 or BL-532
