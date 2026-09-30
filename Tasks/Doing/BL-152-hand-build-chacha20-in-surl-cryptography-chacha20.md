---
id: BL-152
title: Hand-build ChaCha20 in Surl.Cryptography.ChaCha20
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-149]
touches: [Surl.Cryptography.ChaCha20.UnitLibrary, Surl.Cryptography.ChaCha20.UnitTests]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-152 — Hand-build ChaCha20 in Surl.Cryptography.ChaCha20

## Goal

`Surl.Cryptography.ChaCha20` computes the ChaCha20 block function and keystream XOR in both
the RFC 8439 form (96-bit nonce, 32-bit counter) and the original form with a 64-bit nonce
and 64-bit counter that OpenSSH's `chacha20-poly1305@openssh.com` uses, so BL-169 can build
that cipher.

## Context

- Decision: BL-148's ADR (this library references nothing).
- RFC 8439 sections 2.1 to 2.4 define the quarter round, the block function and encryption.
  OpenSSH's `PROTOCOL.chacha20poly1305` (in the OpenSSH source tree) uses the 64-bit nonce
  form: the packet sequence number as the nonce, block counter 0 for the Poly1305 key and 1
  onwards for the payload. Offer both through one internal block function.
- Shape: a static `ChaCha20` with the XOR operation for each form (key 32 bytes, nonce, initial
  counter, input -> output span), and the single-block form BL-169 needs for the Poly1305 key.
  State what happens when the counter would wrap (throw, per form).
- The Curl port's `ChaCha20.cs` may be copied as code; expected values come from published
  sources only (ADR-0003).

## Acceptance criteria

- [ ] Tests pass RFC 8439 section 2.1.1 (quarter round), 2.2.1 (block function), 2.4.2
      (encryption of the "sunscreen" plaintext) and the Appendix A.1 and A.2 vectors; each
      expected value copied from the RFC and cited beside it.
- [ ] Tests pass at least two published vectors for the 64-bit-nonce form with a non-zero
      nonce (for example from draft-strombergson-chacha-test-vectors or from D. J.
      Bernstein's ChaCha reference), the source named beside each.
- [ ] A test shows the counter-wrap behaviour stated in the XML doc.
- [ ] `dotnet build Surl.Cryptography.ChaCha20.UnitLibrary -warnaserror` is clean; the fast
      tests pass; `Measure-CodeQuality.ps1 -Library Surl.Cryptography.ChaCha20.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
