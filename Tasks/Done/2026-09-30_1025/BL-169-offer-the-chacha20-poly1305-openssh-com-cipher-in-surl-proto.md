---
id: BL-169
title: Offer the chacha20-poly1305@openssh.com cipher in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-161, BL-152, BL-153]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-30
---
# BL-169 — Offer the chacha20-poly1305@openssh.com cipher in Surl.Protocol.Ssh

## Goal

`SshProtocolServer` offers and runs `chacha20-poly1305@openssh.com` packet protection over the
hand-built ChaCha20 and Poly1305, in BL-154's ADR's preference position.

## Context

- Decision: BL-154's ADR (cipher list and order); BL-148's ADR (the libraries, and that this
  composition lives in `Surl.Protocol.Ssh`).
- Specification: OpenSSH's `PROTOCOL.chacha20poly1305`: 64 bytes of key material split into
  K_2 (payload, first 32) and K_1 (length, last 32); the 4-byte packet length encrypted with K_1
  and the sequence number as a 64-bit nonce; the Poly1305 key from K_2's block counter 0; the
  payload from counter 1; the 16-byte tag over the encrypted length and ciphertext, checked
  with `CryptographicOperations.FixedTimeEquals` before anything is decrypted. No MAC is
  negotiated with it (the MAC name-list entry is ignored), as the document says.
- Add the `ProjectReference`s to `Surl.Cryptography.ChaCha20.UnitLibrary` and
  `Surl.Cryptography.Poly1305.UnitLibrary`.
- Code to copy (never expectations): the Curl port's
  `PacketProtection/ChaCha20Poly1305PacketProtection.cs`.

## Acceptance criteria

- [x] A fast test round-trips packets under `chacha20-poly1305@openssh.com` with the test-side
      client; a flipped length, ciphertext or tag byte ends the connection with the ADR's
      `DISCONNECT` before any payload is used.
- [x] If a published test vector for the OpenSSH construction exists (for example in an IETF
      SSHM draft), a test pins it, cited; otherwise the Notes say none was found and that
      BL-172's pinned-curl run is the external proof.
- [x] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no socket opened;
      `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- Built as `SshChaCha20Poly1305Protection` behind `SshPacketProtection.Create`; the offer
  already listed the cipher first (ADR-0051 decision 2), so only the protection was missing.
- Seam change: `OpenHead` now takes the sequence number, because the length is encrypted with
  K_1 under the sequence-number nonce, and `HeadLength` is virtual so this cipher reads only
  the 4-byte length first (a whole 8-byte block is the default for encrypted lengths).
  `OpenBody` re-encrypts the plain length to rebuild the tag input rather than holding state
  between the two calls. Block size 8 and the length kept out of alignment, as OpenSSH's
  `packet.c` does for an AEAD cipher.
- A flipped length byte: one that keeps the length aligned shifts where the tag is read, so
  the tag fails and the answer is `DISCONNECT` 5 (`MAC error`); one that breaks alignment is
  refused as badly framed, `DISCONNECT` 2, before the body is read (ADR-0051 decision 9).
  Both are pinned in `SshPacketProtectionTests`.
- Test vector: draft-ietf-sshm-chacha20-poly1305-04, Appendix A (64-byte key, sequence
  number 7), pinned both ways in `SshChaCha20Poly1305ProtectionTests`; the server's output
  matched the draft's bytes on the first run. BL-172's pinned-curl run remains the proof
  against upstream curl.
- The test client's side (`SshTestChaCha20Poly1305`) is written from `PROTOCOL.chacha20poly1305`
  over the ChaCha20 and Poly1305 primitives (pinned to RFC 8439 in their own test projects),
  sharing no code with the server's composition.
- The "packet protection not implemented" refusal is still reachable for a cipher with no
  protection; its two tests now use `aes128-cbc` added to the offer (`OfferWithAnUnbuiltCipher`).
- `Create` went to complexity 12 with the new branch; the AES-CTR and HMAC case moved to
  `CreateCipherAndMac`.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. SshProtocolServer runs chacha20-poly1305@openssh.com over the hand-built ChaCha20 and Poly1305, pinned to the IETF draft's Appendix A vector
