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
completed:
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

- [ ] The client and server Initial keys, IVs and header-protection keys derived from Appendix A's
      Destination Connection ID `0x8394c8f03e515708` equal Appendix A.1's values.
- [ ] Protecting Appendix A.2's client Initial and A.3's server Initial gives the appendix's
      protected bytes, and unprotecting them gives back the plaintext.
- [ ] Appendix A.4's Retry integrity tag and A.5's ChaCha20-Poly1305 short-header packet are
      reproduced, the latter if the ADR supports ChaCha20-Poly1305.
- [ ] A packet whose AEAD tag fails to open is reported as undecryptable (to be dropped, RFC 9001
      section 5.5), not thrown, and has a named test.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
