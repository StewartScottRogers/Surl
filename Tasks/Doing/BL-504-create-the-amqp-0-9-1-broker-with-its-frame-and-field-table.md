---
id: BL-504
title: Create the AMQP 0-9-1 broker with its frame and field-table codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-503, BL-404]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-504 — Create the AMQP 0-9-1 broker with its frame and field-table codec

## Goal

`Surl.Extended.Protocol.Amqp0.UnitLibrary` exists. It checks the protocol header, reads and writes method, content
header, body and heartbeat frames with their frame-end octet, and encodes every AMQP 0-9-1 data type, including
field tables and arrays as the errata define them.

## Context

- Decision: BL-503's ADR, decisions 1 and 8. Rules: BL-543's ADR.
- New projects in `Protocols/Messaging/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.Amqp0`.
  References: `Surl.Protocol.Abstractions` only.
- AMQP 0-9-1:
  - 4.2.2 (protocol header);
  - 4.2.3 (frame format and frame-end `0xCE`; an invalid frame-end is a connection exception 501 FRAME_ERROR);
  - 4.2.5 (data types);
  - 4.2.6 (content frames);
  - 4.2.7 (heartbeat frames on channel 0 only).
  The errata's field-value type octets (`t b B s u I i L l f d D S x A T F V`) as RabbitMQ lists them.

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - `AMQP\x00\x00\x09\x01` accepted;
      - any other header answered with the supported header and closed;
      - each frame type round-tripped;
      - a bad frame-end refused as 501;
      - a frame larger than frame-max refused;
      - each field-value type round-tripped in a field table, nested tables and arrays included;
      - a heartbeat on a channel other than 0 refused.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
