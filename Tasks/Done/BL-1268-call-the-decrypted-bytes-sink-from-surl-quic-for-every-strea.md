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
completed: 2026-11-16
---
# BL-1268 — Call the decrypted-bytes sink from Surl.Quic

## Goal

`Surl.Quic`, given an `IDecryptedBytesSink`, reports each received and sent STREAM frame payload (with its stream id) and DATAGRAM frame payload as `Decrypted` bytes.

## Context

ADR-0169 decision 1. The sink is an optional constructor or options argument defaulting to the discarding sink, so existing callers compile. CRYPTO, ACK and other control frames and all secrets are never reported (decision 5). Packets sent are byte-identical to before.

## Acceptance criteria

- [x] A test: a received STREAM frame's payload reaches `Received(Decrypted, streamId, bytes)` in stream order.
- [x] A test: a sent STREAM payload and a DATAGRAM payload (stream id null) reach `Sent`.
- [x] A test: CRYPTO and ACK frames report nothing.
- [x] A test: the packets sent are identical with the discarding sink and a recording one.
- [x] 100% line and branch coverage kept; `dotnet build` clean.

## Notes

Decided: the sink is `QuicServerConnectionOptions.DecryptedBytes` (default `DiscardingDecryptedBytesSink`). Received STREAM bytes are reported as they leave the reorder buffer (stream order, so a gap-filling frame reports the whole run once); sent bytes when `NextFrame` cuts a frame (a retransmission is not reported twice); empty payloads (FIN only) report nothing. DATAGRAM payloads report with stream id null. "Packets identical" is tested as identical STREAM frames, because the server draws random connection IDs and keys, so raw packets differ run to run.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Quic reports STREAM and DATAGRAM payloads to the decrypted-bytes sink
