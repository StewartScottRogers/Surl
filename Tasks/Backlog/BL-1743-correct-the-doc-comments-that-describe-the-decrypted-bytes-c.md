---
id: BL-1743
title: Correct the doc comments that describe the decrypted-bytes contract as QUIC and DTLS plaintext only
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests/ServingEngineTests.DecryptedBytes.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-1743 — Correct the doc comments that describe the decrypted-bytes contract as QUIC and DTLS plaintext only

## Goal

The comments on the decrypted-bytes contract and on `EventPublishingDecryptedBytesSink` say truthfully who
reports through it, what each layer holds and which logged bytes are redacted.

## Context

- Found while delivering BL-1740. ADR-0169 is the contract; ADR-0182 is the redaction.
- All paths are under `Foundation/`.
- `Surl.Protocol.Abstractions.UnitLibrary/IDecryptedBytesSink.cs`, `ExchangeContext.cs` (on `DecryptedBytes`),
  `Surl.Core.UnitLibrary/EventPublishingDecryptedBytesSink.cs` and
  `Surl.Core.UnitTests/ServingEngineTests.DecryptedBytes.cs` name only QUIC or DTLS code as reporters.
  `Http3RequestStreamServer` and `Http3RequestStream` (`Surl.Http3`) report too: decoded header lists as
  `Decoded`, `DATA` payloads as `Decrypted`.
- `ExchangeBytesLayer.cs` calls `Decrypted` "the plaintext the QUIC or DTLS layer recovered, never reordered or
  reframed". It also holds sent bytes, and HTTP/3 `DATA` payloads with their framing removed.
- `EventPublishingDecryptedBytesSink.cs` says "the note carries them redacted when the server redacts"; its
  `Report` redacts received bytes only and notes sent bytes as they are.

## Acceptance criteria

- [ ] None of the four files says or implies that only QUIC or DTLS code reports through the contract.
- [ ] The summary of `ExchangeBytesLayer.Decrypted` is true of every `Received` and `Sent` call that passes it.
- [ ] The summary of `EventPublishingDecryptedBytesSink` says only received bytes are redacted in the log note.
- [ ] Only comments change; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
