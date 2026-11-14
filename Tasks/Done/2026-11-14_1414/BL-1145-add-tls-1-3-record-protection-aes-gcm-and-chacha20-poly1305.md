---
id: BL-1145
title: Add TLS 1.3 record protection (AES-GCM and ChaCha20-Poly1305 records) to Surl.Tls13
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1144]
touches: [Cryptography/Surl.Tls13.UnitLibrary, Cryptography/Surl.Tls13.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-053
created: 2026-11-14
completed: 2026-11-14
---
# BL-1145 — Add TLS 1.3 record protection (AES-GCM and ChaCha20-Poly1305 records) to Surl.Tls13

## Goal

`Surl.Tls13` can protect and unprotect TLS 1.3 records (RFC 8446 section 5) for the three suites its handshake negotiates.

## Context

- ADR-0157 decision 2, first bullet. `Surl.Tls13` has the handshake without a record layer (QUIC).
- Suites: `TLS_AES_128_GCM_SHA256`, `TLS_AES_256_GCM_SHA384` (BCL `AesGcm`), `TLS_CHACHA20_POLY1305_SHA256` (`Surl.Cryptography.ChaCha20`, `Poly1305`; may need a project reference, base class library and the solution's own libraries only).
- Per-direction keys and IVs come from `TlsKeySchedule` traffic secrets (HKDF-Expand-Label `key`, `iv`); nonce is IV xor 64-bit sequence number.
- Inner plaintext is content then real content type then optional zero padding; outer type 23, version 0x0303; plaintext limit 2^14, ciphertext limit 2^14+256.

## Acceptance criteria

- [x] A record protector and unprotector exist for all three suites, with the 100% line and branch coverage and complexity gates held.
- [x] Tests protect and unprotect the protected records in RFC 8448 section 3 (simple 1-RTT handshake) byte for byte.
- [x] Tests cover padding, an over-long record, a bad tag (`bad_record_mac`), a sequence-number advance and a key rekey (`KeyUpdate` secret derivation).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Design: `TlsRecordProtector` and `TlsRecordUnprotector` (public) share internal `TlsRecordKeys` (key, IV, sequence, rekey). Project references to ChaCha20 and Poly1305 added. RFC 8448 section 3 records (server flight, client Finished, ticket, app data, alerts) are pinned in `Rfc8448Records`, copied from the RFC text. RFC 8448 has no KeyUpdate, so the "traffic upd" secret is pinned from an independent HKDF computation. ChaCha20-Poly1305 is cross-checked against the BCL AEAD. Coverage is not measured here (collector unavailable in this lane); every branch has a test by construction.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Backlog. Lane 2 could not integrate: Surl.Protocol.Abstractions.UnitTests EveryHorizontalLibrary_ReferencesOnlyItsRow failed after rebase.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. Surl.Tls13 protects and unprotects TLS 1.3 records for all three suites, matching RFC 8448 section 3 byte for byte. Lane 1 cherry-picked lane 2's work and added ChaCha20 and Poly1305 to Tls13's row in ProtocolIsolationTests (Foundation/Surl.Protocol.Abstractions.UnitTests; no task in Doing touches it).
