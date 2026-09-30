---
id: BL-248
title: Read DSA host keys with --allow-weak-ssh-algorithms in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-221]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-30
completed:
---
# BL-248 — Read DSA host keys with --allow-weak-ssh-algorithms in Surl.Protocol.Ssh

## Goal

With `--allow-weak-ssh-algorithms`, `SshHostKeyFile.Read` reads a DSA private key (PKCS #8 and
`openssh-key-v1` `ssh-dss`) into the `ssh-dss` host key BL-221 serves, instead of refusing it as
`key type ssh-dss is not supported`.

## Context

- ADR-0051 decision 4: key types are "RSA (at least 2048 bits), ECDSA on P-256, P-384 or P-521,
  Ed25519, and DSA only with `--allow-weak-ssh-algorithms`". BL-221 built the `ssh-dss` host key
  (`SshDsaHostKey`, `SshHostKey.FromDsa`, which requires a 160-bit q) and its signatures, but
  left the file reader's refusal (`SshHostKeyFile.DsaRefusal`) as it was, to stay inside its
  acceptance criteria.
- Code: `Surl.Protocol.Ssh.UnitLibrary/SshHostKeyFile.cs` (`DecodePkcs8`, `DsaRefusal`) and
  `SshOpenSshKeyDecoder.cs` (`ReadPrivateSection`: the `ssh-dss` private section is mpint p, q,
  g, y, x; OpenSSH `sshkey.c`). A DSA key whose q is not 160 bits is refused with
  `key type ssh-dss is not supported`, since `ssh-dss` signatures hold only 160-bit r and s.
- Tests: `SshHostKeyFileTests.Read_DsaKey_IsRefused` (its "With weak algorithms" row changes);
  `SshKeyFileBuilder` builds the files.

## Acceptance criteria

- [ ] With `allowWeakAlgorithms: true`, a PKCS #8 DSA 1024-bit key and an `openssh-key-v1`
      `ssh-dss` key are read, and each signs `ssh-dss` so the BCL `DSA` of the same key verifies it.
- [ ] Without it, both are still refused `DSA keys of 1024 bits need --allow-weak-ssh-algorithms`.
- [ ] A DSA key with a 256-bit q, or an `openssh-key-v1` `ssh-dss` section whose y is not g^x mod p,
      is refused with a typed refusal, never a crash.
- [ ] `dotnet build` and `dotnet test --filter "TestCategory!=Integration"` are green;
      `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

Filed by BL-221 (2026-09-30).

## Log

- 2026-09-30: Created.
