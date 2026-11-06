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
completed: 2026-11-05
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

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - `AMQP\x00\x00\x09\x01` accepted;
      - any other header answered with the supported header and closed;
      - each frame type round-tripped;
      - a bad frame-end refused as 501;
      - a frame larger than frame-max refused;
      - each field-value type round-tripped in a field table, nested tables and arrays included;
      - a heartbeat on a channel other than 0 refused.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Tests naming each criterion: `Amqp0ProtocolHeaderTests.ReadAsync_Amqp0091Header_IsAcceptedWithNothingWritten`,
  `ReadAsync_AnyOtherHeader_IsAnsweredWithTheSupportedHeaderAndClosed`;
  `Amqp0FrameReaderTests.ReadFrameAsync_EachFrameType_RoundTrips`, `ReadFrameAsync_BadFrameEnd_IsRefusedAs501`,
  `ReadFrameAsync_FrameLargerThanFrameMax_IsRefusedAs501FromItsHeader`,
  `ReadFrameAsync_HeartbeatOffChannelZero_IsRefusedAs501`;
  `Amqp0WireReaderTests.ReadFieldTable_EachFieldValueType_RoundTripsInATable`,
  `ReadFieldTable_NestedTablesAndArrays_RoundTrip`. 100 tests; the library measured 100% line and branch
  with the MSTest coverage collector (cobertura).
- Choices (defaults taken, within ADR-0112):
  - No `IConnectionProtocolServer` yet: the broker class arrives with the connection class in BL-505. The header
    check is `Amqp0ProtocolHeader.ReadAsync`, which on a refusal writes `AMQP 0 0 9 1` and completes writes; the
    caller closes.
  - frame-max counts the whole frame, header and frame-end included, as the XML's `frame-max` field says; the
    reader judges type, size and heartbeat channel from the 7-byte header before reading the payload.
  - `l` and `L` both decode as signed 64-bit integers and keep their octet on re-encoding: RabbitMQ's clients send
    `l`, the PDF names `L` `long-long-int`. The PDF's `U` (its signed 16-bit) and its `s` short string are not in
    the errata list, so they are unknown octets (502).
  - Table and array nesting is capped at 64 levels below the outermost table so a hostile frame cannot exhaust
    the stack; deeper is 502.
  - A content header's weight is read and dropped and written as 0; a second property-flags short is 502,
    since `basic`, the only content class, has 14 properties.
- No ADR needed: every rule is ADR-0112's or the specification's.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Extended.Protocol.Amqp0 checks the AMQP 0-9-1 protocol header, reads and writes every frame type with 501 refusals, and encodes every data type and errata field-value type
