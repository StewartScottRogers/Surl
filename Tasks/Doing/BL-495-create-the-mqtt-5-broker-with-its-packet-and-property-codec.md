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
completed:
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

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - each packet type round-tripped with every property it allows;
      - the Variable Byte Integer boundaries (127, 128, 16383, 16384, 268435455) and a fifth byte refused;
      - a UTF-8 string with U+0000 or an unpaired surrogate refused with `0x81`;
      - reserved fixed-header flags set refused;
      - a duplicate property refused with `0x82`;
      - a property not allowed in its packet refused;
      - a packet split across reads reassembled.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
