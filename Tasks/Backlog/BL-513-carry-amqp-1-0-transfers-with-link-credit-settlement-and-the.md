---
id: BL-513
title: Carry AMQP 1.0 transfers with link credit, settlement and the message format
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-512]
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

## Acceptance criteria

- [ ] Named tests show:
      - a message sent in three `more` frames stored whole and accepted;
      - a receiving link with credit 2 getting two of three stored messages, then the third after more credit;
      - a pre-settled delivery not redelivered;
      - an unsettled delivery released by the receiver redelivered;
      - a rejected one not redelivered;
      - an aborted transfer discarded;
      - a transfer beyond credit refused with the right condition;
      - the message sections kept byte for byte from sender to receiver.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
