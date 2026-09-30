---
id: BL-223
title: Read encrypted OpenSSH host keys with --pass in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-160, BL-161, BL-169, BL-220]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions/ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-30
---
# BL-223 — Read encrypted OpenSSH host keys with --pass in Surl.Protocol.Ssh

## Goal

The SSH host-key reader decrypts an `openssh-key-v1` private key written with a passphrase
(`kdfname` `bcrypt`), using the passphrase `--pass` gives, instead of refusing it with ADR-0051's
"encrypted OpenSSH keys are not available in this build" refusal.

## Context

- Decision: ADR-0051 (BL-154) - the `--pass` option (parsed by BL-158), the encrypted-key
  ciphers read, and the typed refusals: no passphrase given, wrong passphrase, unsupported
  `kdfname` or `ciphername`, malformed key. It defines the "not available in this build"
  refusal this task replaces. The reader is BL-160's; Surl.Console already hands it the
  passphrase or turns its refusals into exit codes (BL-171), so the refusal texts stay
  ADR-0051's.
- Specification: OpenSSH `PROTOCOL.key` (openssh-portable, tag `V_9_9_P1`): the
  `openssh-key-v1\0` magic, `ciphername`, `kdfname`, `kdfoptions` (salt string and uint32
  rounds for `bcrypt`), the key count, the public keys, then the private section encrypted as
  a whole and padded to the cipher's block size. Key and IV come from one
  `bcrypt_pbkdf(passphrase, salt, rounds)` output of key length plus IV length
  (`Surl.Cryptography.BcryptPbkdf`, BL-220; add the `ProjectReference`). A wrong passphrase
  shows as the two `checkint` values of the decrypted section differing.
- Ciphers: `aes256-ctr` (ssh-keygen's default), `aes128-ctr` and `aes192-ctr` (BL-161's
  AES-CTR), `aes256-gcm@openssh.com` (BL-161's AES-GCM; the tag follows the encrypted section)
  and `chacha20-poly1305@openssh.com` (BL-169's cipher), all of which `ssh-keygen -Z` writes;
  any other `ciphername` gets ADR-0051's unsupported-cipher refusal.
- Fixtures: keys written by `ssh-keygen -N <passphrase>` (and `-Z <cipher>`, `-a <rounds>` with
  a low round count to keep tests fast), the `ssh-keygen` version named in the test file;
  test-only keys and passphrases.

## Acceptance criteria

- [x] For each cipher in Context, a fast test reads an encrypted fixture with its passphrase and
      the key it yields signs an exchange hash the test-side client verifies with the fixture's
      public key.
- [x] Fast tests show a wrong passphrase refused by the `checkint` mismatch, a missing
      passphrase, an unsupported `kdfname` and `ciphername`, and a truncated key each refused
      with ADR-0051's typed refusal.
- [x] No code path returns the "encrypted OpenSSH keys are not available in this build" refusal
      any more; `grep` for its text in `Surl.Protocol.Ssh.UnitLibrary` finds nothing.
- [x] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror`
      is clean; `dotnet build` and `dotnet test --filter "TestCategory!=Integration"` are green
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and 100% branch coverage, no method above cyclomatic complexity 10, and
      no failing member.

## Notes

- BL-169 was added to `depends-on` because the `chacha20-poly1305@openssh.com` key cipher uses
  its implementation.
- `Documentation/Planning/Decisions/ADR-0051-...md` was added to `touches`: its decision 4 and
  refusal table promised the "not available in this build" refusal this task removes, and BL-223's
  own choices belong beside them. No task in `Doing` names it (BL-171 touches Surl.Cli and
  Surl.Console only).
- Plan and what was built: `SshOpenSshKeyDecryption` (new) reads the `bcrypt` KDF options,
  derives key and IV with `BcryptPbkdf.DeriveKey`, and opens the section: `aes*-gcm@openssh.com`
  with the BCL's `AesGcm` (the IV is the whole nonce, no AAD, the tag after the section),
  `chacha20-poly1305@openssh.com` with ChaCha20/Poly1305 at sequence number 0 and no length field
  (OpenSSH's `chachapoly_crypt` with `aadlen` 0), and every non-AEAD cipher through the transport's
  own `SshCipherAlgorithm`. `SshOpenSshKeyDecoder.Decode` now takes `--pass`; a check-integer
  mismatch is "--pass does not decrypt the key" for an encrypted key and "not a private key"
  for an unencrypted one. The `EncryptedOpenSshKey` refusal and its enum member are gone.
- Decisions (recorded in ADR-0051 decision 4, decided by Claude under Stewart's delegation):
  an unsupported `ciphername` or `kdfname` is "not a private key surl can read" (the ADR's table
  has no separate words and needs none); an empty `--pass` is "--pass does not decrypt the key"
  (OpenSSH treats an empty passphrase as wrong; `bcrypt_pbkdf` takes no empty password); the CBC
  and `arcfour` ciphers are read too, since the transport already builds them and older OpenSSH
  wrote them; the passphrase is UTF-8, as for PKCS #8.
- Fixtures: `Surl.Protocol.Ssh.UnitTests/Fixtures/openssh-encrypted-keys`, written by
  `ssh-keygen` from OpenSSH_10.3p1 (Git for Windows) with `-a 2`: Ed25519 under each of the six
  `-Z` ciphers plus `aes256-cbc`, and RSA 2048 and ECDSA P-384 under `aes256-ctr`. Commands are in
  the fixtures' README. `SshTestKeyExchangeClient.SignatureVerifies` became `internal` so the
  tests verify signatures with the test-side client's verifier.
- Gates: `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` gives 100% line, 100%
  branch, 605 members, 0 failing, worst CRAP 10 (`Decode` first measured at complexity 14, so
  `IsEncrypted` was split out). Surl.Protocol.Ssh.UnitTests: 959 passed.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Encrypted openssh-key-v1 host keys (bcrypt KDF; AES-CTR, AES-GCM, ChaCha20-Poly1305, CBC) are decrypted with --pass
