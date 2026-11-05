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
completed: 2026-11-05
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

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - each format code of Part 1 section 1.6 read and written;
      - a described list read with a symbolic and with a numeric descriptor;
      - a performative with trailing null fields omitted read as the defaults;
      - a frame with DOFF greater than 2 read, skipping the extended header;
      - an empty frame read as a heartbeat;
      - a frame over `max-frame-size` refused.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Delivered by Claude, 2026-11-05. The library holds the type system, the frame codec, the 14
  performatives as records, `error`, and the message sections; the library's CLAUDE.md lists them.
- Where the acceptance criteria are shown (all in `Surl.Extended.Protocol.Amqp1.UnitTests`):
  - each format code read and written: `Amqp1FormatCodeTests` (every primitive, binary and
    composite code; the one-octet boolean `0x56` read, and written inside an array; wide forms of
    small values read and written in the smallest form);
  - a described list with a numeric and a symbolic descriptor:
    `Amqp1DecoderTests.ReadValue_DescribedList_ReadsTheDescriptorAndTheList` and
    `Amqp1FrameBodyTests.Read_PerformativeWithASymbolicDescriptor_IsRead`;
  - trailing null fields read as defaults:
    `Amqp1FrameBodyTests.Read_OpenWithTrailingNullFieldsOmitted_ReadsTheDefaults` and
    `Read_AttachWithTrailingNullFieldsOmitted_ReadsTheDefaults`;
  - DOFF above 2: `Amqp1FrameReaderTests.ReadFrameAsync_DataOffsetAbove2_SkipsTheExtendedHeader`;
  - empty frame: `Amqp1FrameReaderTests.ReadFrameAsync_EmptyFrame_IsReadAsAHeartbeat`;
  - over `max-frame-size`:
    `Amqp1FrameReaderTests.ReadFrameAsync_FrameOverMaxFrameSize_IsRefusedWithoutReadingItsBody`.
- Gates: `dotnet build -warnaserror` clean; fast tests green (185 in the new project);
  `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Amqp1.UnitLibrary` reports 100% line,
  100% branch, highest complexity 10, 0 failing members.
- Choices taken by default (codec details inside ADR-0107's decisions, so no new ADR):
  - Values decode to plain .NET types (`uint`, `string`, `Rune` for `char`, `Guid` for `uuid`,
    `byte[]` for `binary`) plus small records where .NET has no type (`Amqp1Symbol`,
    `Amqp1Timestamp`, the three decimals kept as raw bits, `Amqp1Map`, `Amqp1Array`,
    `Amqp1Described`). Every width of a type reads as the same .NET type, so a field accepts
    `uint0`, `smalluint` and `uint`, and `list0`, `list8` and `list32`, alike.
  - The encoder always writes the smallest encoding; an `Amqp1Array` keeps the element format code
    it was read with, so arrays round-trip byte for byte. That is the only way `0x56` is written.
  - Nesting limit 64 (described values, lists, maps, arrays), as Amqp0's table limit; an array of
    a zero-width element may claim at most 65536 elements, so a few bytes cannot ask for billions.
  - A `symbol` field with `multiple="true"` is written as one symbol when it holds one and as an
    array when it holds more; either is read.
  - Performative records hold effective values (a field left out reads as its default) and write a
    field equal to its default as null, then drop trailing nulls.
  - `source`, `target`, delivery states, message-id and correlation-id stay the values read;
    BL-512 and BL-513 give source, target and delivery states types.
  - `Amqp1DecodeException.Condition` is `amqp:decode-error`, or `amqp:connection:framing-error`
    for a body that is not a performative of its frame's type (ADR-0107 decision 3).
  - `Documentation/Conformance/Extended/` has no Amqp1 page yet; it is outside this task's
    `touches` and belongs with the composition (BL-514).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Extended.Protocol.Amqp1 reads and writes every AMQP 1.0 type, the performatives, message sections, and AMQP and SASL frames
