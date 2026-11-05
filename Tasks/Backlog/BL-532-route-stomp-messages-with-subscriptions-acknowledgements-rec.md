---
id: BL-532
title: Route STOMP messages with subscriptions, acknowledgements, receipts, transactions and heart-beats
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-531]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-532 — Route STOMP messages with subscriptions, acknowledgements, receipts, transactions and heart-beats

## Goal

The STOMP broker routes SEND frames to subscribers as MESSAGE frames, with the destination semantics BL-530's ADR chose.
It honours the three ack modes, NACK, receipts, transactions and DISCONNECT, and sends and checks heart-beats.

## Context

- Decision: BL-530's ADR, decisions 4 to 7.
- STOMP 1.2: SEND, SUBSCRIBE (`id`, `ack`), UNSUBSCRIBE, ACK and NACK (the `id` header in 1.2), BEGIN, COMMIT
  and ABORT, DISCONNECT, MESSAGE (`destination`, `message-id`, `subscription`, and `ack` when the mode needs one),
  RECEIPT (`receipt-id`), and "Heart-beating".
- Destinations and subscriptions are shared by connections and owned by the server instance. Time from
  `ExchangeContext.TimeProvider`.

## Acceptance criteria

- [ ] Named tests show:
      - SEND delivered as MESSAGE with the specification's headers;
      - `client` ack acknowledging cumulatively and `client-individual` one message at a time;
      - an unacknowledged message redelivered after the subscriber disconnects, as the ADR says;
      - NACK handled as the ADR says;
      - a `receipt` header answered with RECEIPT;
      - a transaction's SENDs delivered only on COMMIT and dropped on ABORT;
      - DISCONNECT with a receipt answered before the close;
      - heart-beat EOLs sent at the negotiated interval;
      - a silent client closed after the ADR's tolerance.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
