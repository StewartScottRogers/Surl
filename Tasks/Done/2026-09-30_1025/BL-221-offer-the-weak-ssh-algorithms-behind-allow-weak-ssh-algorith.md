---
id: BL-221
title: Offer the weak SSH algorithms behind --allow-weak-ssh-algorithms in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-161, BL-162, BL-224]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions/ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-30
---
# BL-221 — Offer the weak SSH algorithms behind --allow-weak-ssh-algorithms in Surl.Protocol.Ssh

## Goal

When the SSH server's options say weak algorithms are allowed (`--allow-weak-ssh-algorithms`,
ADR-0051), `SshProtocolServer` also offers and completes the SHA-1 and 1024-bit key exchanges,
the `ssh-rsa` and `ssh-dss` signatures, the CBC, 3DES and RC4 ciphers and the SHA-1 and MD5
MACs listed below; without it, none of them is offered or accepted.

## Context

- Decision: ADR-0051 (BL-154) - the option, the order each weak name takes in its name-list,
  and the typed refusals for a weak host key or user key given without the option. Option
  parsing is BL-158's; this task adds the setting to the SSH server's options (the record
  BL-160 and BL-161 read their algorithm lists from) and honours it. Passing `SurlCommandLine`'s
  value into it is `Surl.Console`'s composition (BL-171); if BL-171 is already Done and does not
  pass it, file a follow-up task rather than widening this one.
- Key exchange: `diffie-hellman-group14-sha1` (RFC 4253 section 8, the 2048-bit MODP group of
  RFC 3526 section 3), `diffie-hellman-group1-sha1` (RFC 4253 section 8.1, Oakley group 2 of
  RFC 2409 section 6.2), `diffie-hellman-group-exchange-sha1` (RFC 4419, SHA-1 in place of
  SHA-256 on BL-160's group-exchange path). SHA-1 is the BCL's `SHA1`.
- Signatures, host key and user key: `ssh-rsa` (RSASSA-PKCS1-v1_5 with SHA-1, RFC 4253
  section 6.6, BCL `RSA`) and `ssh-dss` (RFC 4253 section 6.6: 1024-bit DSA with SHA-1, the
  signature blob the 40-byte r || s, BCL `DSA` with
  `DSASignatureFormat.IeeeP1363FixedFieldConcatenation`); RSA keys shorter than 2048 bits are
  accepted only with the option. The weak signature names join `server-sig-algs` in `EXT_INFO`
  (RFC 8308 section 3.1) only with the option.
- Ciphers (RFC 4253 section 6.3): `aes256-cbc`, `rijndael-cbc@lysator.liu.se` (the same cipher
  as `aes256-cbc` under OpenSSH's older name), `aes192-cbc`, `aes128-cbc` (BCL `Aes` in CBC
  mode, the IV chained from packet to packet), `3des-cbc` (BCL `TripleDES`, 24-byte key),
  `arcfour` (RFC 4253, 128-bit key, no discard) and `arcfour128` (RFC 4345, discard 1536),
  both from `Surl.Cryptography.Rc4` (BL-224; add the `ProjectReference`).
- MACs (RFC 4253 section 6.4): `hmac-sha1`, `hmac-sha1-etm@openssh.com` (OpenSSH `PROTOCOL`
  section 1.7, on BL-161's encrypt-then-MAC path), `hmac-sha1-96`, `hmac-md5`, `hmac-md5-96`
  (the `-96` forms the first 12 bytes), BCL `HMACSHA1` and `HMACMD5`, compared with
  `CryptographicOperations.FixedTimeEquals` as BL-161 does.
- Code to copy (never expectations, ADR-0003): the Curl port's `PacketProtection/` and its key
  exchange types. The test-side client is BL-160's.

## Acceptance criteria

- [x] With the option off, a fast test reads the server's `KEXINIT` and finds none of the names
      above in any name-list, nor in `server-sig-algs`; a client offering only one of them is
      disconnected as ADR-0051 says.
- [x] With the option on, the `KEXINIT` name-lists and `server-sig-algs` contain each name above
      in ADR-0051's order.
- [x] With the option on, a fast test completes each of the three key exchanges with the
      test-side client, and the client verifies an `ssh-rsa` (SHA-1) and an `ssh-dss` host-key
      signature and a 1024-bit RSA host key's signature.
- [x] Fast tests accept a valid `ssh-rsa` and `ssh-dss` user-key signature with the option on,
      refuse each flipped signature, and refuse both, and a 1024-bit RSA user key, with the
      option off, as ADR-0051 says.
- [x] AES-CBC passes NIST SP 800-38A section F.2's CBC vectors (F.2.1, F.2.3, F.2.5), and the
      `-96` MACs match RFC 2202's test case 5 truncated digests for HMAC-SHA-1 and HMAC-MD5,
      each value cited beside its test.
- [x] With the option on, each cipher and each MAC above round-trips packets with the test-side
      client, and a flipped ciphertext or MAC byte ends the connection with BL-161's
      `DISCONNECT`.
- [x] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror`
      is clean; `dotnet build` and `dotnet test --filter "TestCategory!=Integration"` are green
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and 100% branch coverage, no method above cyclomatic complexity 10, and
      no failing member.

## Notes

- Built: `SshAlgorithmOffer.Default(..., allowWeakAlgorithms)` and `AllowsWeakAlgorithms`;
  `SshModpGroup.Oakley2` (RFC 2409 group 2) and the three SHA-1 key exchanges in
  `SshKeyExchangeMethod`; `ssh-rsa` signing on `SshRsaHostKey`, a new `SshDsaHostKey` with
  `SshHostKey.FromDsa`; `ssh-rsa`/`ssh-dss` verification and the 2048-bit RSA floor in
  `SshUserKeySignature.AcceptsKey`, and `server-sig-algs` from `AlgorithmsFor`; the cipher seam
  `ISshPacketCipher` with `SshAesCtr`, `SshCbc` (AES, 3DES) and `SshArcfour` (BL-224's `Rc4`,
  `ProjectReference` added), keyed through `SshCipherAlgorithm`; `SshHmac` gains `MacLength` and
  the SHA-1/MD5 MACs.
- Decisions (ADR-0051 section 2.3, added here, decided by Claude under Stewart's delegation):
  the setting lives on the offer; without it a weak user key is refused as `USERAUTH_FAILURE`
  before the policy, like an unverified algorithm; every RSA host key can sign `ssh-rsa`, which
  the offer lists only with the option; stream ciphers pad to 8 bytes.
- `touches` gained ADR-0051: the behaviour choices above belong in it, and no task in Doing
  (BL-182, BL-204) names it.
- Left out, filed: reading a DSA host key from a `--hostkey` file is BL-248 (the reader still
  refuses it with the option as "not supported"). BL-171 (Backlog) now says to pass
  `AllowWeakSshAlgorithms` into the offer and the host-key reader.
- Tests that used weak names as "not built" examples now use `twofish256-cbc`,
  `hmac-ripemd160` and `diffie-hellman-group15-sha512`, which ADR-0051 does not offer. The user
  authentication tests' RSA key moved from 1024 to 2048 bits, since 1024 is now refused without
  the option.
- No new upstream curl bytes were pinned: ADR-0051's measured lists already name every weak entry,
  and each pinned build prefers a strong one. The test-side client runs CBC block by block over
  the BCL's ECB and RC4 with `Surl.Cryptography.Rc4`, so no test checks the server against its
  own cipher code. A derived 3DES key that happened to be a DES weak key would make the BCL throw;
  the chance is about 2^-64 per key and is not handled.
- Results: `Surl.Protocol.Ssh.UnitTests` 934 passed; the whole fast run green;
  `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` 100% line, 100% branch,
  0 failing members, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Protocol.Ssh offers and completes the SHA-1/1024-bit kex, ssh-rsa/ssh-dss, CBC/3DES/RC4 ciphers and SHA-1/MD5 MACs only with --allow-weak-ssh-algorithms
