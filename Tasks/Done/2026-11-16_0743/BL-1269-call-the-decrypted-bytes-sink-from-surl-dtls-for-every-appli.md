---
id: BL-1269
title: Call the decrypted-bytes sink from Surl.Dtls for every application-data record (ADR-0169)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1266]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests]
requirement: FR-060
created: 2026-11-16
completed: 2026-11-16
---
# BL-1269 — Call the decrypted-bytes sink from Surl.Dtls

## Goal

`Surl.Dtls`, given an `IDecryptedBytesSink`, reports each application-data record's plaintext, received and sent, as `Decrypted` bytes with no stream id.

## Context

ADR-0169 decision 1 and 5. Handshake, alert and change-cipher-spec records, keys and PSKs are never reported. The sink is optional and defaults to discarding; records on the wire are byte-identical to before.

## Acceptance criteria

- [x] A test: a received application-data record's plaintext reaches `Received(Decrypted, null, bytes)`.
- [x] A test: a sent application-data record's plaintext reaches `Sent`.
- [x] A test: handshake and alert records report nothing.
- [x] A test: records sent are identical with the discarding sink and a recording one.
- [x] 100% line and branch coverage kept; `dotnet build` clean.

## Notes

Added `DtlsServerOptions.DecryptedBytes` (init, default discarding sink); `DtlsServerSession` reports on the three application-data paths (established receive, set-aside receive during a replacing handshake, `SendAsync`). The "identical records" test compares header and length, since each handshake draws fresh keys.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Surl.Dtls reports application-data plaintext to an optional IDecryptedBytesSink
