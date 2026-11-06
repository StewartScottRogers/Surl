---
id: BL-896
title: Cap a WebTransport session's waiting datagram echoes at 64
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-460]
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-896 — Cap a WebTransport session's waiting datagram echoes at 64

## Goal

A WebTransport session holds at most 64 HTTP datagrams waiting to be echoed, dropping the newest past
that, as ADR-0117 decision 8's table says.

## Context

- ADR-0117 decision 8, row "datagrams waiting to be echoed": 64 a session, the newest dropped.
- Today `WebTransportSession.EchoDatagramsAsync` takes each datagram from `Surl.Http3`'s
  `Http3ExtendedConnectSession.ReceiveDatagramAsync`, whose own queue holds
  `ReceivedDatagramCapacity` (128); the WebTransport server applies no cap of its own.
- Conformance row `WebTransport:Surl-8-3` in `Documentation/Conformance/Extended/WebTransport.md`
  (its stream half, 65536 bytes a stream, is `WebTransportSession.EchoChunkLength`).
- Found while finishing BL-460.

## Acceptance criteria

- [ ] A named test in `Surl.Extended.Protocol.WebTransport.UnitTests` shows a 65th datagram waiting
      for its echo dropped while the first 64 are echoed.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
