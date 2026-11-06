---
id: BL-453
title: Create the Socket.IO server with its Engine.IO and Socket.IO packet codecs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-452, BL-404]
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests, Surl.slnx, Documentation/Conformance/Extended/SocketIo.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests encode and decode each example in the two specifications' encoding sections byte for byte. They
      cover a namespace other than `/`, a packet with an ack id, a binary event with two attachments, and a
      malformed packet refused.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Touches: added `Documentation/Conformance/Extended/SocketIo.md`. ADR-0120's Consequences make each task
  replace its ID in that requirement matrix with the named test; no task in Doing touches it.
- Scope: the codecs cover ADR-0120 decisions 3 (open packet, the 20-character base64url ids through an
  injected `RandomNumberGenerator`) and 6 (placeholders), and the packet limits of decision 7
  (namespace 128 bytes, ack id 15 digits, JSON nesting 64, attachments 1 to 64). The 64-attachment
  cap is enforced in the reader as well, so BL-454 only maps a refusal to Close `1002`; the matrix
  keeps Surl-7-1 on BL-454 because its size half needs the session.
- The HTTP long-polling encoding (`b` plus base64, packets joined at `0x1E`) is built here too,
  because EIO "Packet encoding" is one section for both transports and it costs a few lines; BL-762
  serves the transport.
- Choice: a namespace with no `,` after it (`1/admin`) is refused. SIO "Format" writes the namespace
  as `<namespace>,`, every example has the comma, and a strict reading cannot mistake a payload for
  part of a namespace. Recorded here; ADR-0120 decision 7 already makes malformed packets protocol
  errors, so no new ADR.
- Choice: the payload shape per type is checked in the codec (CONNECT absent or an object; DISCONNECT
  none; EVENT a non-empty array with a string first; ACK an array with an ack id; CONNECT_ERROR an
  object), as SIO "Packet encoding" and ADR-0120 decisions 4 and 5 say. That lets BL-454 treat every
  `TryReadFromClient` refusal as one protocol error. An ack id is read only on EVENT, ACK and their
  binary forms; on other types the digits fall to the payload and fail its shape.
- Coverage: the library is at 100% line and 100% branch (cobertura from `dotnet test --collect "Code Coverage"`).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Engine.IO v4 and Socket.IO v5 packet codecs built and tested; library at 100% line and branch coverage
