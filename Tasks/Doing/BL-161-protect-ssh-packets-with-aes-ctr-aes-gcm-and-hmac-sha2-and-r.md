---
id: BL-161
title: Protect SSH packets with AES-CTR, AES-GCM and HMAC-SHA2 and rekey in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-160]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-161 — Protect SSH packets with AES-CTR, AES-GCM and HMAC-SHA2 and rekey in Surl.Protocol.Ssh

## Goal

After `NEWKEYS`, `SshProtocolServer` encrypts and authenticates every packet with the
BCL-built ciphers and MACs BL-154's ADR lists (AES-CTR, AES-GCM, HMAC-SHA2 and their
encrypt-then-MAC forms), and re-keys when the client or the ADR's limits ask.

## Context

- Decision: BL-154's ADR (cipher and MAC lists, order, re-key limits, `DISCONNECT` codes).
- Specifications: RFC 4344 (`aes128-ctr`, `aes192-ctr`, `aes256-ctr`, composed from
  `Aes.EncryptEcb`, BL-148's ADR); RFC 5647 as OpenSSH's `PROTOCOL` modifies it
  (`aes128-gcm@openssh.com`, `aes256-gcm@openssh.com`, with `AesGcm`; offered only where
  `AesGcm.IsSupported`, as the ADR decides); RFC 6668 (`hmac-sha2-256`, `hmac-sha2-512`) and
  OpenSSH's `-etm@openssh.com` forms; RFC 4253 section 6.4 (MAC over the sequence number) and
  section 9 (re-exchange). A received MAC or tag is compared with
  `CryptographicOperations.FixedTimeEquals`; a failure ends the connection with the ADR's
  `DISCONNECT`.
- The sequence number wraps at 2^32 (RFC 4253 section 6.4); strict kex resets it if BL-154's
  ADR decided strict kex.
- Code to copy (never expectations): the Curl port's `PacketProtection/`
  (`AesCtrSshCipher`, `AesGcmPacketProtection`, `CipherAndMacPacketProtection`, `SshMac`).

## Acceptance criteria

- [ ] AES-CTR passes NIST SP 800-38A section F.5's CTR vectors (F.5.1, F.5.3, F.5.5), cited.
- [ ] For each cipher and MAC combination this task covers, a fast test round-trips packets with
      the test-side client of BL-160, and a flipped ciphertext or MAC byte ends the connection
      with the ADR's `DISCONNECT`.
- [ ] A fast test re-keys mid-session (client `KEXINIT` after `NEWKEYS`) and the session goes on
      under the new keys.
- [ ] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
