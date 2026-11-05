---
id: BL-374
title: Protect and unprotect QUIC packets in Surl.Quic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-373]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-374 — Protect and unprotect QUIC packets in Surl.Quic

## Goal

`Surl.Quic` protects and unprotects QUIC packets (RFC 9001 section 5): it derives Initial secrets from
the client's Destination Connection ID, derives packet and header-protection keys from a traffic
secret, seals and opens payloads with the AEADs that BL-372's ADR chose, and applies and removes
header protection.

## Context

- Decision: BL-372's ADR, decision 3. It fixes the AEADs: AES-128-GCM always, and AES-256-GCM and
  ChaCha20-Poly1305 if the ADR supports them. It also fixes whether the BCL's `AesGcm` and
  `ChaCha20Poly1305` are used or the hand-built `Surl.Cryptography.ChaCha20` and
  `Surl.Cryptography.Poly1305` (ADR-0048). A reference to those libraries needs the ADR's
  ADR-0002 row.
- RFC 9001: section 5.1 (`HKDF-Expand-Label` with `quic key`, `quic iv` and `quic hp`), 5.2 (initial
  secrets, salt `0x38762cf7f55934b34d179ae6a4c80cadccbb7f0a`), 5.3 (nonce), 5.4 (header protection:
  AES-ECB and ChaCha20 samples), and 5.8 (Retry integrity tag). Appendix A has test vectors for every
  step.
- BCL: `HKDF` (`System.Security.Cryptography`), `AesGcm`, and `Aes.EncryptEcb`.
- Builds on BL-373's packet headers.

## Acceptance criteria

- [x] The client and server Initial keys, IVs and header-protection keys derived from Appendix A's
      Destination Connection ID `0x8394c8f03e515708` equal Appendix A.1's values.
- [x] Protecting Appendix A.2's client Initial and A.3's server Initial gives the appendix's
      protected bytes, and unprotecting them gives back the plaintext.
- [x] Appendix A.4's Retry integrity tag and A.5's ChaCha20-Poly1305 short-header packet are
      reproduced, the latter if the ADR supports ChaCha20-Poly1305.
- [x] A packet whose AEAD tag fails to open is reported as undecryptable (to be dropped, RFC 9001
      section 5.5), not thrown, and has a named test.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- Per ADR-0082 decision 3: AES-128/256-GCM and AES-ECB header protection are the BCL's
  (`AesGcm`, `Aes.EncryptEcb`); ChaCha20-Poly1305 and the ChaCha20 mask are the hand-built
  `Surl.Cryptography.ChaCha20` and `Surl.Cryptography.Poly1305`. The references to them and to
  `Surl.Tls13` (for `TlsCipherSuite`, `TlsTrafficSecrets` and `HKDF-Expand-Label`) are the ADR's
  ADR-0002 row for `Surl.Quic.UnitLibrary`. The two ciphers sit behind an internal
  `QuicPacketCipher` so `QuicPacketProtection` has one path for both.
- `Unprotect` reports three outcomes as `QuicUnprotectStatus` rather than throwing: `Unprotected`,
  `Undecryptable` (tag fails, or too short to sample - drop, RFC 9001 sections 5.4.2 and 5.5) and
  `ReservedBitsNotZero` (opened, but a `PROTOCOL_VIOLATION` per RFC 9000 section 17.2, which can
  only be checked after the tag succeeds). A header that cannot even be located still throws
  `QuicTransportException` (`PROTOCOL_VIOLATION`, no frame type), as BL-373's reader does.
  `PacketLength` is returned in every case so the caller reads the next coalesced packet.
- Full packet numbers are recovered by `QuicPacketNumber.Decode` (RFC 9000 Appendix A.3).
- `QuicRetryIntegrity` computes and checks Retry tags; Surl sends no Retry (ADR-0082 decision 4).
- Every expected value comes from RFC 9001 Appendix A (A.1 to A.5) and RFC 9000 Appendix A.3,
  copied from the RFC text into `Rfc9001AppendixA.cs`; nothing from the Curl port. Upstream curl
  was not measured: packet protection has no curl-visible choice the RFC's vectors leave open.
- Coverage of `Surl.Quic.UnitLibrary` measured with the MSTest coverage collector: line-rate 1,
  branch-rate 1. Fast tests: 44 assemblies passed, none failed; `Surl.Quic.UnitTests` 190.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Quic derives Initial and traffic keys, protects and unprotects packets with AES-GCM and ChaCha20-Poly1305 and checks Retry tags, reproducing RFC 9001 Appendix A
