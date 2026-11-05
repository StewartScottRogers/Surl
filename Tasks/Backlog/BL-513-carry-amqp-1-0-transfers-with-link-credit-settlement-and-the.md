---
id: BL-513
title: Carry AMQP 1.0 transfers with link credit, settlement and the message format
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-512, BL-664, BL-704]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-513 — Carry AMQP 1.0 transfers with link credit, settlement and the message format

## Goal

The AMQP 1.0 container grants credit to sending links, accepts transfers (multi-frame included) into its nodes,
and settles them as BL-509's ADR decides. It delivers stored messages to receiving links as their credit
allows, and handles the dispositions the receiver sends.

## Context

- Decision: BL-509's ADR, decisions 5 and 6.
- AMQP 1.0:
  - Part 2, section 2.6.7 (flow and link credit), 2.6.12 and 2.6.13 (transferring messages and settlement), and
    2.7.5 (transfer: `delivery-id`, `delivery-tag`, `more`, `settled`, `aborted`);
  - section 2.7.6 (disposition);
  - Part 3, section 3.2 (message sections: header, delivery-annotations, message-annotations, properties,
    application-properties, data / amqp-sequence / amqp-value, footer) and 3.4 (delivery states).
- Node state is shared by connections and owned by the server instance, not a static.
- **Shared message bus (Stewart, 2026-11-04; BL-622's ADR, applied by BL-509's ADR):** nodes are the bus's topics
  and queues (BL-631), addresses converted with BL-630's mapping, so a message published over `mqtt` reaches an
  `amqp1` receiver and the reverse. The library references `Surl.MessageBus.UnitLibrary`. The message sections are
  kept byte for byte for an `amqp1` receiver; what other protocols see of them is BL-622's ADR's. Each attach passes
  the connection's account and scheme to BL-664's access check, a refusal answered `amqp:unauthorized-access` as
  BL-509's ADR says.

## Acceptance criteria

- [ ] Named tests show:
      - a message sent in three `more` frames stored whole and accepted;
      - a receiving link with credit 2 getting two of three stored messages, then the third after more credit;
      - a pre-settled delivery not redelivered;
      - an unsettled delivery released by the receiver redelivered;
      - a rejected one not redelivered;
      - an aborted transfer discarded;
      - a transfer beyond credit refused with the right condition;
      - the message sections kept byte for byte from sender to receiver;
      - a message published on the bus under an MQTT name delivered to a receiver attached to the mapped address;
      - an attach refused by topic access answered as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-664; nodes are Surl.MessageBus's topics and queues.
- 2026-11-05: Depends on BL-704 (the bus carries the native form ADR-0107 decision 6 needs), added by BL-509.
