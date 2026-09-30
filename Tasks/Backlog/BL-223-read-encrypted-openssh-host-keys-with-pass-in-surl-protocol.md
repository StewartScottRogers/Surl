---
id: BL-223
title: Read encrypted OpenSSH host keys with --pass in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-160, BL-161, BL-169, BL-220]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-29
completed:
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

- [ ] For each cipher in Context, a fast test reads an encrypted fixture with its passphrase and
      the key it yields signs an exchange hash the test-side client verifies with the fixture's
      public key.
- [ ] Fast tests show a wrong passphrase refused by the `checkint` mismatch, a missing
      passphrase, an unsupported `kdfname` and `ciphername`, and a truncated key each refused
      with ADR-0051's typed refusal.
- [ ] No code path returns the "encrypted OpenSSH keys are not available in this build" refusal
      any more; `grep` for its text in `Surl.Protocol.Ssh.UnitLibrary` finds nothing.
- [ ] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror`
      is clean; `dotnet build` and `dotnet test --filter "TestCategory!=Integration"` are green
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and 100% branch coverage, no method above cyclomatic complexity 10, and
      no failing member.

## Notes

- BL-169 was added to `depends-on` because the `chacha20-poly1305@openssh.com` key cipher uses
  its implementation.

## Log

- 2026-09-29: Created.
