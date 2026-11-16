---
id: BL-1268
title: Call the decrypted-bytes sink from Surl.Quic for every STREAM and DATAGRAM payload (ADR-0169)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1266]
touches: [Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests]
requirement: FR-060
created: 2026-11-16
completed:
---
# BL-1268 — Call the decrypted-bytes sink from Surl.Quic

## Goal

`Surl.Quic`, given an `IDecryptedBytesSink`, reports each received and sent STREAM frame payload (with its stream id) and DATAGRAM frame payload as `Decrypted` bytes.

## Context

ADR-0169 decision 1. The sink is an optional constructor or options argument defaulting to the discarding sink, so existing callers compile. CRYPTO, ACK and other control frames and all secrets are never reported (decision 5). Packets sent are byte-identical to before.

## Acceptance criteria

- [ ] A test: a received STREAM frame's payload reaches `Received(Decrypted, streamId, bytes)` in stream order.
- [ ] A test: a sent STREAM payload and a DATAGRAM payload (stream id null) reach `Sent`.
- [ ] A test: CRYPTO and ACK frames report nothing.
- [ ] A test: the packets sent are identical with the discarding sink and a recording one.
- [ ] 100% line and branch coverage kept; `dotnet build` clean.

## Notes

## Log

- 2026-11-16: Created.
