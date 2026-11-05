---
id: BL-453
title: Create the Socket.IO server with its Engine.IO and Socket.IO packet codecs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-452, BL-404]
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-453 — Create the Socket.IO server with its Engine.IO and Socket.IO packet codecs

## Goal

`Surl.Extended.Protocol.SocketIo.UnitLibrary` exists. It encodes and decodes every Engine.IO v4 packet and every
Socket.IO v5 packet, including the namespace, the ack id, JSON data and binary attachment placeholders. It needs
no transport yet.

## Context

- Decision: BL-452's ADR, decisions 3 and 6. Rules: BL-543's ADR.
- New projects in `Protocols/Http/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.SocketIo`. References:
  `Surl.Protocol.Abstractions` only; BL-454 adds the WebSocket library.
- The packet encodings and their examples: the Engine.IO v4 "Encoding" section and the Socket.IO v5 "Packet
  encoding" section (for example `2["hello",1]`, `21["hello"]`, `3/admin,1[]`, `451-["event",{"_placeholder":true,"num":0}]`).
- JSON through `Utf8JsonReader`/`Utf8JsonWriter` (native AOT).

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests encode and decode each example in the two specifications' encoding sections byte for byte. They
      cover a namespace other than `/`, a packet with an ack id, a binary event with two attachments, and a
      malformed packet refused.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
