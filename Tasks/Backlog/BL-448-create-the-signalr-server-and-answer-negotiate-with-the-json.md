---
id: BL-448
title: Create the SignalR server and answer negotiate with the JSON hub message codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-447, BL-404, BL-407]
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-448 — Create the SignalR server and answer negotiate with the JSON hub message codec

## Goal

`Surl.Extended.Protocol.SignalR.UnitLibrary` exists. It answers `POST <hub>/negotiate?negotiateVersion=1` with
the negotiate JSON, checks the bearer login, and reads and writes every JSON hub-protocol message with its
record separator.

## Context

- Decision: BL-447's ADR, decisions 1, 2, 3 and 5. Rules: BL-543's ADR.
- New projects in `Protocols/Http/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.SignalR`. References:
  `Surl.Protocol.Abstractions` and BL-407's library. BL-449 adds the WebSocket library.
- TransportProtocols.md "`POST [endpoint-base]/negotiate`" (the response's fields and its `error` form), and
  HubProtocol.md "JSON Encoding" (each message type's fields, the `0x1E` terminator, several messages in one
  frame, and a message split across frames).
- JSON through `Utf8JsonReader`/`Utf8JsonWriter` or a source-generated context (native AOT).

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - negotiate answered with a fresh `connectionId` and `connectionToken` and the ADR's transports;
      - negotiate without a token refused as the ADR says;
      - each message type round-tripped against the JSON examples in HubProtocol.md;
      - two messages in one buffer read separately;
      - a message split across two buffers reassembled;
      - a message over the limit refused.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
