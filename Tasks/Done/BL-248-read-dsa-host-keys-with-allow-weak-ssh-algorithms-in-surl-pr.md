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
completed: 2026-09-30
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

- [x] With `allowWeakAlgorithms: true`, a PKCS #8 DSA 1024-bit key and an `openssh-key-v1`
      `ssh-dss` key are read, and each signs `ssh-dss` so the BCL `DSA` of the same key verifies it.
- [x] Without it, both are still refused `DSA keys of 1024 bits need --allow-weak-ssh-algorithms`.
- [x] A DSA key with a 256-bit q, or an `openssh-key-v1` `ssh-dss` section whose y is not g^x mod p,
      is refused with a typed refusal, never a crash.
- [x] `dotnet build` and `dotnet test --filter "TestCategory!=Integration"` are green;
      `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

Filed by BL-221 (2026-09-30).

Delivered directly rather than through the full `/feature` agent chain: the task names the two
methods to change and every refusal, so there was nothing left for a plan to decide.

- `SshHostKeyFile.DsaRefusal` is gone. `RequireWeakAlgorithmsForDsa` refuses on p alone before
  the rest is read, so a DSA key without `--allow-weak-ssh-algorithms` is still refused for its
  size whatever follows p (the existing p-only fixtures keep passing).
- `SshHostKeyFile.FromDsa(p, q, g, y?, x)` is shared by PKCS #8 (RFC 3279: Dss-Parms p, q, g;
  x as an INTEGER; y computed) and `openssh-key-v1` (p, q, g, y, x; y must equal g^x mod p).
  The values are parsed by hand rather than by `DSA.ImportPkcs8PrivateKey`, so every platform
  refuses a damaged key the same way. Choices (defaults, no ADR needed - they follow ADR-0051
  decision 4 and the task's own criteria):
  - q not 160 bits -> `key type ssh-dss is not supported` (UnsupportedKeyType), as the task says.
  - out-of-range values (not 1 < q < p, 1 < g < p, 0 < x < q), a g whose order is not q, or a
    y that is not g^x mod p -> `not a private key surl can read`, as the ECDSA and Ed25519
    readers already answer a key whose public half does not match its private half.
  - `DSA.Create(parameters)` runs while reading, so a size the platform cannot sign with is
    refused at startup rather than at the first handshake.
- Tests: `SshHostKeyFileTests` - `Read_DsaKeyWithoutWeakAlgorithms_IsRefusedAsWeak`,
  `Read_DsaKeyWithWeakAlgorithms_SignsSshDssThatTheKeyVerifies` (PKCS #8, encrypted PKCS #8,
  OpenSSH), `Read_DsaKeyWithA256BitQ_IsRefusedAsNotSupported`,
  `Read_DamagedDsaKeyWithWeakAlgorithms_IsNotAPrivateKey` (8 cases). `SshKeyFileBuilder` gained
  `Pkcs8Dsa(p, q, g, x)` and `OpenSshDsaFields`.
- Measured: Surl.Protocol.Ssh.UnitLibrary 100% line, 100% branch, 0 failing members, worst
  CRAP 10; Surl.Protocol.Ssh.UnitTests 977 passed.
- ADR-0051 line 301 ("Reading a DSA key from a `--hostkey` file is BL-248's") stays true and is
  outside this task's `touches`, so it was left as is.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl reads PKCS #8 and openssh-key-v1 DSA host keys as ssh-dss with --allow-weak-ssh-algorithms
