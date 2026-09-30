---
id: BL-231
title: Record BL-160's SSH key exchange and host-key reading choices in an ADR
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-160]
touches: [Documentation/Planning/Decisions]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-231 — Record BL-160's SSH key exchange and host-key reading choices in an ADR

## Goal

An ADR amending ADR-0051, marked "Decided by Claude under Stewart's delegation", records the
key exchange and host-key reading choices BL-160 made where ADR-0051 is silent, each with its
reason, and ADR-0051's Consequences point to it.

## Context

- BL-160 built the choices below in `Surl.Protocol.Ssh.UnitLibrary` and wrote them in its
  task Notes; it could not write the ADR itself because BL-173 held
  `Documentation/Planning/Decisions` at the time.
- The choices, all as the code now does (read `SshHostKeyFile`, `SshPkcs8Decryption`,
  `SshOpenSshKeyDecoder`, `SshDiffieHellman`, `SshEcdhKeyExchange`,
  `SshGroupExchangeKeyExchange`, `SshPacketReader`, `SshPacketWriter`):
  1. A client public value off the curve, or not in 1 < e < p - 1, is `DISCONNECT` 2
     (ADR-0051 decision 9's "malformed message"), not 3.
  2. `SSH_MSG_KEX_DH_GEX_REQUEST_OLD` (30) is not answered: `DISCONNECT` 2. libssh2 sends
     the three-value request (34).
  3. The server's finite-field private exponent is 512 bits from `ISshRandomSource`; the ECDH
     ephemeral key comes from `ECDiffieHellman.Create(curve)`, the platform's generator.
  4. Encrypted PKCS #8 is decrypted by hand from BCL primitives (PBES2, PBKDF2 over
     HMAC-SHA-1/256/384/512, AES-128/192/256-CBC), not with `ImportEncryptedPkcs8PrivateKey`,
     so an encrypted DSA or Ed25519 key is refused for its type rather than its passphrase.
     Other schemes (PBES1, 3DES) are "not a private key surl can read".
  5. A legacy encrypted PEM (`Proc-Type: 4,ENCRYPTED` headers) is "not a private key surl
     can read".
  6. Until BL-168, an Ed25519 host key is "key type ssh-ed25519 is not supported"; until
     BL-221, a DSA key given with `--allow-weak-ssh-algorithms` is "key type ssh-dss is not
     supported" (without it, decision 4's weak-key refusal).
  7. An unsupported PKCS #8 algorithm is named by its object identifier; an EC key on another
     curve as `ecdsa on curve <oid>`.
  8. Only the client's sequence number is refused on wrap during a strict first exchange; the
     server writes four packets there, so its own cannot wrap.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` records choices 1 to 8 above, each
      with its reason, is marked "Decided by Claude under Stewart's delegation", and amends
      ADR-0051.
- [ ] ADR-0051's Consequences (or an Amended-by line) names the new ADR.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR if it lists the others.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
