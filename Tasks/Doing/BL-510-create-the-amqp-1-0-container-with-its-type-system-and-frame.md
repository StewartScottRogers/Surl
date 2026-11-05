---
id: BL-510
title: Create the AMQP 1.0 container with its type system and frame codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-509, BL-404]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-510 — Create the AMQP 1.0 container with its type system and frame codec

## Goal

`Surl.Extended.Protocol.Amqp1.UnitLibrary` exists. It encodes and decodes every AMQP 1.0 primitive type in each
of its widths, described types, lists, maps and arrays, the performatives and message sections as described
lists, and the AMQP and SASL frames.

## Context

- Decision: BL-509's ADR. Rules: BL-543's ADR.
- New projects in `Protocols/Messaging/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.Amqp1`.
  References: `Surl.Protocol.Abstractions` only.
- AMQP 1.0:
  - Part 1, sections 1.2 to 1.6 (type system, the encodings table with each format code, the zero-width forms
    `0x40`, `0x41`, `0x42`, `0x43` and `0x44`, and composite types as described lists);
  - Part 2, section 2.3 (frame layout: size, DOFF, type `0x00` AMQP and `0x01` SASL, channel, and the empty
    frame) and section 2.7 (each performative's descriptor and fields);
  - Part 3, section 3.2 (message sections).
  Where a field allows several encodings, the reader accepts every one: list8 and list32, small and wide
  integers, and a single symbol where the field is multiple.

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - each format code of Part 1 section 1.6 read and written;
      - a described list read with a symbolic and with a numeric descriptor;
      - a performative with trailing null fields omitted read as the defaults;
      - a frame with DOFF greater than 2 read, skipping the extended header;
      - an empty frame read as a heartbeat;
      - a frame over `max-frame-size` refused.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
