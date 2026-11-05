---
id: BL-495
title: Create the MQTT 5 broker with its packet and property codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-494, BL-404]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-495 — Create the MQTT 5 broker with its packet and property codec

## Goal

`Surl.Extended.Protocol.Mqtt5.UnitLibrary` exists. It encodes and decodes all fifteen MQTT 5 control packets with
their properties and reason codes, the data representations of chapter 1.5, and the fixed header. It reports each
malformation as the reason code chapter 4.13 names.

## Context

- Decision: BL-494's ADR. Rules: BL-543's ADR.
- New projects in `Protocols/Messaging/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.Mqtt5`.
  References: `Surl.Protocol.Abstractions` only for now.
- MQTT 5.0:
  - 1.5 (Two Byte and Four Byte Integers, UTF-8 strings with `[MQTT-1.5.4-1]` to `[MQTT-1.5.4-3]`, the Variable
    Byte Integer, Binary Data, String Pairs);
  - 2.1 (the fixed header and reserved flags);
  - 2.2.2 (properties: identifiers, the types, and a property appearing twice being a Protocol Error);
  - 2.4 (reason codes);
  - each packet's chapter 3 layout.
- The `Surl.Protocol.Mqtt` 3.1.1 codec (`MqttPacketReader`, `MqttPacketEncoder`) may be copied and extended; it may
  not be referenced.

## Acceptance criteria

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - each packet type round-tripped with every property it allows;
      - the Variable Byte Integer boundaries (127, 128, 16383, 16384, 268435455) and a fifth byte refused;
      - a UTF-8 string with U+0000 or an unpaired surrogate refused with `0x81`;
      - reserved fixed-header flags set refused;
      - a duplicate property refused with `0x82`;
      - a property not allowed in its packet refused;
      - a packet split across reads reassembled.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- **What was built.** `Mqtt5PacketEncoder.Encode` / `Mqtt5PacketDecoder.Decode` for all fifteen packets (one
  record per layout: `Mqtt5ConnectPacket`, `Mqtt5ConnectAcknowledgementPacket`, `Mqtt5PublishPacket`,
  `Mqtt5PublishResponsePacket` for PUBACK/PUBREC/PUBREL/PUBCOMP, `Mqtt5SubscribePacket`,
  `Mqtt5ReasonCodeListPacket` for SUBACK/UNSUBACK, `Mqtt5UnsubscribePacket`, `Mqtt5PingPacket`,
  `Mqtt5ReasonCodeAndPropertiesPacket` for DISCONNECT/AUTH); `Mqtt5BodyReader` / `Mqtt5BodyWriter` for chapter 1.5
  and property lists; table 2-4 as `Mqtt5PropertyDefinition`; `Mqtt5PacketReader` (copied from `MqttPacketReader`)
  for reads off an `IConnection`, with the packet limit and the PUBLISH payload limit of ADR-0093 decision 8.
- **Where the tests are.** Round trips with every allowed property: `Mqtt5PacketEncoderTests.Encode_*_RoundTrips`
  (each read back one byte per read). VBI boundaries and the fifth byte:
  `Mqtt5BodyReaderTests.ReadVariableByteInteger_BoundaryOfTable1_1_ReadsIt`, `..._FifthByte_IsMalformed`, and
  `Mqtt5BodyWriterTests.WriteVariableByteInteger_BoundaryOfTable1_1_WritesTheFewestBytes`. U+0000 and surrogates:
  `Mqtt5BodyReaderTests.ReadUtf8String_NullCharacter_IsMalformed0x81`, `..._SurrogateOrIllFormed_IsMalformed0x81`.
  Reserved flags: `Mqtt5PacketDecoderTests.Decode_ReservedFixedHeaderFlagsSet_IsMalformed0x81`. Duplicate:
  `Mqtt5BodyReaderTests.ReadProperties_DuplicateProperty_IsProtocolError0x82`. Not allowed:
  `Mqtt5BodyReaderTests.ReadProperties_PropertyNotAllowedInItsPacket_IsMalformed0x81`,
  `Mqtt5PacketDecoderTests.Decode_ConnectWithAPropertyOnlyConnackCarries_IsMalformed0x81`. Split reads:
  `Mqtt5PacketReaderTests.ReadPacketAsync_PacketSplitAcrossReads_ReassemblesIt`. 139 tests; library at 100% line,
  100% branch, worst CRAP 10.
- **Decision: the specification over ADR-0093's table, in two rows.** MQTT 5.0 section 2.2.2.2 makes a property
  identifier not valid for its packet a Malformed Packet (`0x81`), and 3.8.3 / 3.10.3 make an empty SUBSCRIBE /
  UNSUBSCRIBE a Protocol Error (`0x82`); ADR-0093 decision 6 had both the other way round. The specification is
  the oracle for an extended server (ADR-0083), so the codec follows it. Amending the ADR needs
  `Documentation/Planning/Decisions`, which BL-470 holds in Doing, so it is filed as BL-651 rather than added to
  this task's `touches`.
- **Choices with a sensible default.** An undefined property identifier is `0x81` (its type is unknown, so the
  rest cannot be parsed). Packet Identifier 0 is refused `0x82` in every packet that carries one, not only in
  SUBSCRIBE, UNSUBSCRIBE and PUBLISH, since no acknowledgement can match it. A CONNECT naming `MQTT` or `MQIsdp` at
  another level reports `Mqtt5PacketFault.UnsupportedProtocolLevel`, any other name `UnknownProtocolName` - both
  `0x84`, kept apart so BL-496 can close without bytes for the second as ADR-0093 decision 1 says. Value checks
  (Receive Maximum 0, Payload Format Indicator above 1, Subscription Identifier 0, Topic Filter syntax, which
  packets a client may send) are left to the broker tasks BL-496 to BL-499; the codec is structural.
- `Surl.Protocol.Abstractions` is the only reference, as the task's Context says; `Surl.Content`, `Surl.HttpMessage`
  and `Surl.WebSocket` arrive with the tasks that need them.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Mqtt5 codec encodes and decodes all 15 MQTT 5 packets with properties and reports each malformation with its 4.13 reason code
