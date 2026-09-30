---
id: BL-160
title: Run the SSH key exchanges and host-key signatures the BCL provides in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-159]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-160 — Run the SSH key exchanges and host-key signatures the BCL provides in Surl.Protocol.Ssh

## Goal

`SshProtocolServer` completes the key exchanges BL-154's ADR assigns to the BCL, signs the
exchange hash with a BCL host key, derives the session keys and exchanges `SSH_MSG_NEWKEYS`,
and reads its host keys from the bytes of the files BL-154's ADR names.

## Context

- Decision: BL-154's ADR (which kex methods and host-key algorithms the BCL covers, their
  order, the host-key file formats, strict kex if decided).
- Specifications: RFC 4253 sections 7.2 (key derivation), 8 (Diffie-Hellman, the exchange hash
  H, the session identifier); RFC 5656 section 4 (`ecdh-sha2-nistp256/384/521`) and section 3
  (`ecdsa-sha2-nistp*` signatures) with `ECDiffieHellman` and `ECDsa`; RFC 8268
  (`diffie-hellman-group14-sha256`, `group16-sha512`, `group18-sha512`) and RFC 4419
  (`diffie-hellman-group-exchange-sha256`) with `BigInteger.ModPow` and the RFC 3526 primes;
  RFC 8332 (`rsa-sha2-256`, `rsa-sha2-512`) with `RSA`. Only those BL-154's ADR lists.
- The negotiated keys are handed to packet protection, which is BL-161; until then this task
  may end the connection with the ADR's `DISCONNECT` right after `NEWKEYS`, and BL-161 replaces
  that. Reading the host-key file from disk is `Surl.Console`'s (BL-171); this task parses bytes
  and returns typed refusals.
- Tests cannot replay a recorded curl key exchange (the keys are random), so a test-side client
  built from the same BCL primitives completes each kex against the server through
  `InMemoryConnection` and checks the host-key signature over H; randomness is injected. What
  pinned curl negotiates is proved by BL-172.
- Code to copy (never expectations): the Curl port's `KeyExchange/` (`EcdhSshKeyExchange`,
  `FiniteFieldSshKeyExchange`, `GroupExchangeSshKeyExchange`, `SshKeyDerivation`,
  `SshExchangeHashInput`) and `Keys/` (`OpenSshPrivateKeyDecoder`, `EcdsaSshPrivateKey`,
  `RsaSshPrivateKey`).

## Acceptance criteria

- [ ] For each kex method and host-key algorithm this task covers, a fast test completes the
      exchange with the test-side client and verifies the server's signature and that both sides
      derive the same six keys (RFC 4253 section 7.2 letters A to F).
- [ ] Fast tests cover a malformed `KEXDH_INIT`/`KEX_ECDH_INIT` (an off-curve point, a DH value
      outside `1 < e < p-1`), a group-exchange request outside the ADR's bounds, and each
      host-key file refusal the ADR lists.
- [ ] The RFC 4253 section 7.2 derivation is checked against a published vector if one exists
      (cite it), otherwise against a value computed by hand in the test from the formula, and
      the Notes say which.
- [ ] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
