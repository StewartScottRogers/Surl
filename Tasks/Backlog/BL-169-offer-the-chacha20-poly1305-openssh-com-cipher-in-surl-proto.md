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
completed:
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

- [ ] A fast test round-trips packets under `chacha20-poly1305@openssh.com` with the test-side
      client; a flipped length, ciphertext or tag byte ends the connection with the ADR's
      `DISCONNECT` before any payload is used.
- [ ] If a published test vector for the OpenSSH construction exists (for example in an IETF
      SSHM draft), a test pins it, cited; otherwise the Notes say none was found and that
      BL-172's pinned-curl run is the external proof.
- [ ] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no socket opened;
      `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
