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
completed: 2026-09-29
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

- [x] For each kex method and host-key algorithm this task covers, a fast test completes the
      exchange with the test-side client and verifies the server's signature and that both sides
      derive the same six keys (RFC 4253 section 7.2 letters A to F).
- [x] Fast tests cover a malformed `KEXDH_INIT`/`KEX_ECDH_INIT` (an off-curve point, a DH value
      outside `1 < e < p-1`), a group-exchange request outside the ADR's bounds, and each
      host-key file refusal the ADR lists.
- [x] The RFC 4253 section 7.2 derivation is checked against a published vector if one exists
      (cite it), otherwise against a value computed by hand in the test from the formula, and
      the Notes say which.
- [x] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- **What was built.** `SshTransportHandshake` now runs the method agreed after the
  negotiation and exchanges `NEWKEYS` both ways, returning `SshKeyExchangeResult` (algorithms,
  session identifier, `SshKeyDerivation`); `SshProtocolServer` then ends with `DISCONNECT` 11
  "Packet protection not implemented" until BL-161. Methods: `SshEcdhKeyExchange`
  (`ecdh-sha2-nistp256/384/521`, client point checked on the curve by `SshNistCurve`),
  `SshFiniteFieldKeyExchange` (`group14-sha256`, `group16-sha512`, `group18-sha512`),
  `SshGroupExchangeKeyExchange` (`group-exchange-sha256` over `SshModpGroup`'s RFC 3526 groups
  14 to 18). `curve25519-sha256` still ends with `DISCONNECT` 11 "Key exchange not implemented"
  (BL-167). Host keys: `SshHostKey` (public, `FromRsa`/`FromEcdsa`), `SshHostKeySet` (one per
  key type, `TryAdd` names the key already held), `SshHostKeyFile.Read(bytes, passphrase,
  allowWeak)` returning `SshHostKeyReading` with a typed `SshHostKeyRefusal` whose `Text` is
  ADR-0051 decision 4's words after `Host key <path>: `. `SshProtocolServer`'s constructor now
  takes the `SshHostKeySet` first (nothing composes it yet; BL-171 does).
- **Strict kex.** Each packet reader and writer counts its sequence number; under strict kex
  both are set back to 0 after `NEWKEYS`, and the client's is refused on wrap during the first
  exchange. `SshTransportHandshake.PacketReader`/`PacketWriter` are exposed for BL-161.
- **Key derivation vector.** RFC 4253 has no published section 7.2 vector (NIST CAVP's SSH KDF
  vectors exist but are not in the repository and cannot be fetched in this unattended run),
  so `SshKeyDerivationTests` checks values computed by hand in the test from the formula, with
  K written out byte for byte as an `mpint` (a top bit set, so the leading zero byte shows),
  for a key within one hash and one needing two extensions. Independently, each kex test's
  client derives all six keys with its own code and compares them with the server's.
- **RFC 3526 primes.** Groups 15 and 17 are not in the Curl port; they were computed from RFC
  3526's formula and checked prime. `SshModpGroupTests` checks all five against the formula
  with pi computed in the test by Machin's formula, so no constant is taken on trust.
- **Test client.** `SshTestKeyExchangeClient` is built from BCL primitives (ECDH, `BigInteger`,
  RSA/ECDSA verify) and writes H and the derivation out from the RFCs. Its messages do not
  depend on the server's answers (a group exchange predicts group 15 from its 2048/3072/8192
  request), so it runs through `InMemoryConnection`; no socket is opened.
- **Choices made where ADR-0051 is silent** (defaults taken under rule 1; BL-231 records them in
  an ADR, since BL-173 holds `Documentation/Planning/Decisions` during this run): a client value
  off the curve or outside 1 < e < p - 1 is `DISCONNECT` 2 (decision 9's "malformed message");
  `GEX_REQUEST_OLD` (30) is `DISCONNECT` 2; the DH private exponent is 512 bits from
  `ISshRandomSource`, the ECDH ephemeral key from `ECDiffieHellman.Create` (platform RNG);
  encrypted PKCS #8 is decrypted by hand from BCL primitives (PBES2, PBKDF2 HMAC-SHA-1/2,
  AES-CBC) so DSA and Ed25519 are refused for their type, not their passphrase; legacy
  `Proc-Type` encrypted PEM is "not a private key surl can read"; Ed25519 is "not supported"
  until BL-168 and DSA with `--allow-weak-ssh-algorithms` until BL-221; an unknown PKCS #8
  algorithm is named by OID, an EC key on another curve as `ecdsa on curve <oid>`.
- **Review fixes** (code-reviewer): an encrypted PKCS #8 IV that is not 16 bytes is now "not a
  private key surl can read" instead of an escaping `ArgumentException`; the
  `SshProtocolServer` constructor refuses an offer naming a host-key algorithm no key signs;
  strict kex needs both markers (client's `kex-strict-c`, server's `kex-strict-s`), so a
  caller-built offer without the server marker never resets sequence numbers one-sidedly;
  `SshHostKey.FromEcdsa` tells the curve by its public point lying on it, not by key size or
  a platform-dependent curve name; an `openssh-key-v1` ECDSA key's Q is recomputed from d and
  must match the file's; H covers e's bytes as the client sent them; the client's ECDH public
  key object is disposed. Left as they are: no cap on PBKDF2 iterations (the operator's own
  file), trailing bytes after a kex message's fields are ignored (low impact). What pinned
  upstream curl does past `KEXINIT` is BL-172's proof, as the task's Context says.
- **Quality.** `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`: 100% line,
  100% branch, 181 members, 0 failing, worst CRAP 10. 238 tests in `Surl.Protocol.Ssh.UnitTests`.
- **Filed:** BL-231 (the ADR for the choices above).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SshProtocolServer completes ecdh-sha2-nistp256/384/521, diffie-hellman-group14-sha256/16-sha512/18-sha512 and group-exchange-sha256, signs H with rsa-sha2-512/256 or ecdsa-sha2-nistp*, derives the six keys, exchanges NEWKEYS (strict reset), and reads host keys from openssh-key-v1, PKCS#8 (plain and PBES2), PKCS#1 and SEC1 bytes with typed refusals
