---
id: BL-1266
title: Add the decrypted-bytes contract to Surl.Protocol.Abstractions: ExchangeBytesLayer, Layer and StreamId on ExchangeBytesEvent, IDecryptedBytesSink and ExchangeContext.DecryptedBytes (ADR-0169)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1243]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-060
created: 2026-11-16
completed: 2026-11-16
---
# BL-1266 — Add the decrypted-bytes contract to Surl.Protocol.Abstractions

## Goal

`Surl.Protocol.Abstractions` carries ADR-0169 decisions 2 and 3: the layer, the stream identifier and the sink a server hands to QUIC or DTLS code. Nothing publishes yet.

## Context

ADR-0169 decisions 2 and 3. Changes only add: `ExchangeBytesEvent`'s positional constructor stays as it is.

## Acceptance criteria

- [x] `ExchangeBytesLayer` enum (`Wire` = 0, `Decrypted`, `Decoded`) exists with XML docs.
- [x] `ExchangeBytesEvent` gains init properties `Layer` (default `Wire`) and `StreamId` (`long?`, default `null`); a test shows an event built the old way has both defaults.
- [x] `IDecryptedBytesSink` (`Received` and `Sent`, each taking layer, `long? streamId`, `ReadOnlySpan<byte>`) and `DiscardingDecryptedBytesSink.Instance` exist.
- [x] `ExchangeContext.DecryptedBytes` is an init property defaulting to the discarding sink.
- [x] The library and its tests keep 100% line and branch coverage; `dotnet build` is clean.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Decrypted-bytes contract (layer, stream id, sink, ExchangeContext.DecryptedBytes) added with tests
