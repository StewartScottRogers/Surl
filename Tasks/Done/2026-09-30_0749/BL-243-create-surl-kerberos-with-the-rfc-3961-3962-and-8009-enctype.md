---
id: BL-243
title: Create Surl.Kerberos with the RFC 3961, 3962 and 8009 enctype profiles
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-217]
touches: [Surl.Kerberos.UnitLibrary, Surl.Kerberos.UnitTests, Surl.slnx, Surl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions/ADR-0002-mirror-the-curl-ports-project-map.md, Documentation/Product/Product-Overview.md]
requirement: FR-046
created: 2026-09-30
completed: 2026-09-30
---
# BL-243 — Create Surl.Kerberos with the RFC 3961, 3962 and 8009 enctype profiles

## Goal

A new horizontal library pair, `Surl.Kerberos.UnitLibrary` and `Surl.Kerberos.UnitTests`, exists
in `Surl.slnx` and encrypts, decrypts and checksums with the four AES Kerberos enctypes (17, 18, 19,
20), each pinned by the RFCs' published test vectors.

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md`.
  - decision 3: the accepted enctypes are `aes128-cts-hmac-sha1-96` (17), `aes256-cts-hmac-sha1-96`
    (18), `aes128-cts-hmac-sha256-128` (19), `aes256-cts-hmac-sha384-192` (20); `rc4-hmac`, DES,
    3DES and Camellia are not built.
  - decision 6: the library references nothing but the shared framework
    (`System.Security.Cryptography`, `System.Formats.Asn1`), namespace `Surl.Kerberos`, one public
    type per file, AOT-compatible. The public
    `enum KerberosEncryptionType { Aes128CtsHmacSha196 = 17, Aes256CtsHmacSha196 = 18, Aes128CtsHmacSha256128 = 19, Aes256CtsHmacSha384192 = 20 }`
    lands here. Internal: the RFC 3961 simplified profile (`n-fold`, `DK`, `DR`), RFC 3962 AES-CTS
    with HMAC-SHA1-96, RFC 8009 `KDF-HMAC-SHA2` with HMAC-SHA256-128 and HMAC-SHA384-192, CTS built
    over the BCL's `Aes.EncryptCbc`/`Aes.DecryptCbc`, and the checksum types 15, 16, 19 and 20.
    There is no string-to-key (decision 1). `Surl.Authentication.UnitLibrary` will reference this
    library (BL-240), as it references `Surl.Cryptography`.
  - decision 11: the crypto is pinned by RFC 3961 appendix A.1 (`n-fold`), RFC 3962 appendix B
    (AES-CTS encryptions under the key `chicken teriyaki`, and the derived keys of its
    string-to-key cases taken as fixed keys) and RFC 8009 appendix A (key derivations, encryptions
    with their confounders, checksums for enctypes 19 and 20). Every test is fast, with no KDC.
  - Amends ADR-0002 decision 3's table with a `Surl.Kerberos.UnitLibrary` row referencing nothing.
- Create the pair with the `new-project` skill (`.claude/skills/new-project/SKILL.md`), which places
  it in `Surl.slnx` in the flat alphabetical run (between `Surl.Cryptography.UnitTests` and
  `Surl.LineProtocol.UnitLibrary`), `Surl.Kerberos.UnitTests` directly after its library.
- Isolation: `Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs` lists the horizontal
  libraries (e.g. its `Rc4` constant for `Surl.Cryptography.Rc4.UnitLibrary`); add
  `Surl.Kerberos.UnitLibrary` the same way, as a library no protocol server may need and which
  references no protocol server.
- `Documentation/Product/Product-Overview.md`, section "Layers": the Services row says
  `Surl.Authentication` also references `Surl.Cryptography`; it gains that `Surl.Authentication`
  will reference `Surl.Kerberos` for Kerberos inside Negotiate and SASL `GSSAPI` (ADR-0057 decision
  6); the hand-built primitives row gains `Surl.Kerberos` (references nothing); the project-pair
  table gains the `Surl.Kerberos.UnitLibrary` | `Surl.Kerberos.UnitTests` row.
- Base class library only: no package (root `CLAUDE.md`).

## Acceptance criteria

- [x] `Surl.Kerberos.UnitLibrary` and `Surl.Kerberos.UnitTests` exist at the repository root and in
      `Surl.slnx` in sorted position; the library's project file has no `ProjectReference` and no
      `PackageReference`.
- [x] `KerberosEncryptionType` is public in namespace `Surl.Kerberos` with exactly the four members
      and values of ADR-0057 decision 6; every other type added is internal.
- [x] Tests in `Surl.Kerberos.UnitTests` (e.g. `NFoldTests`, `AesCtsHmacSha1EncryptionTests`,
      `AesCtsHmacSha2EncryptionTests`, `KerberosChecksumTests`) reproduce every RFC 3961 appendix
      A.1 `n-fold` vector, every RFC 3962 appendix B AES-CTS encryption and its derived keys used as
      fixed keys, and every RFC 8009 appendix A key derivation, encryption (with its confounder) and
      checksum for enctypes 19 and 20; decryption round-trips each vector and a tampered cipher text
      fails its integrity check (compared with `CryptographicOperations.FixedTimeEquals`) without
      throwing.
- [x] `ADR-0002-mirror-the-curl-ports-project-map.md` decision 3's table has a
      `Surl.Kerberos.UnitLibrary` row referencing nothing, citing ADR-0057.
- [x] `ProtocolIsolationTests` in `Surl.Protocol.Abstractions.UnitTests` names
      `Surl.Kerberos.UnitLibrary` and passes.
- [x] `Documentation/Product/Product-Overview.md` "Layers" states that `Surl.Authentication` will
      reference `Surl.Kerberos` (ADR-0057 decision 6) and lists the new project pair.
- [x] `dotnet build Surl.Kerberos.UnitLibrary -warnaserror` is clean;
      `dotnet test --filter "TestCategory!=Integration"` passes; no test needs
      `TestCategory=Integration`; every test is platform-neutral (no OS-specific path or text).
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch
      coverage, no method over complexity 10 and no CRAP score over 30 for
      `Surl.Kerberos.UnitLibrary`.

## Notes

- The keytab, principal names, AP-REQ check, AP-REP, wrap/MIC tokens and replay cache are BL-239,
  not this task. The profiles stay internal; the tests reach them through
  `<InternalsVisibleTo Include="Surl.Kerberos.UnitTests" />` in the library's project file, as
  `Surl.Authentication.UnitLibrary.csproj` does.
- ADR-0057 decision 6 lets the implementer rename internal members where the code shows a truer
  name; the split and responsibilities stand.
- Built (2026-09-30): `NFold` (RFC 3961 5.1), `SimplifiedProfileKeyDerivation` (`DK`/`DR`),
  `AesCiphertextStealing` (CBC-CS3 over `Aes.EncryptCbc`/`DecryptCbc`/`EncryptEcb`/`DecryptEcb`),
  `KdfHmacSha2`, and `KerberosEncryptionProfile` (abstract; `For(enctype)`, `DeriveKey`, `Encrypt`,
  `TryDecrypt`, `ComputeChecksum`, `VerifyChecksum`) with `AesCtsHmacSha1Profile` and
  `AesCtsHmacSha2Profile`; internal enums `KerberosChecksumType` (15, 16, 19, 20) and
  `KerberosDerivedKeyPurpose` (0x99, 0xAA, 0x55). 85 tests, every RFC vector matched.
- Choice: RFC 3962 appendix B has no whole-message encryption vectors, so enctypes 17/18 are
  pinned by its CTS vectors and by its string-to-key cases' last step, `DK(PBKDF2 output,
  "kerberos")` = the published AES key (the PBKDF2 output taken as a fixed key, as ADR-0057
  decision 11 asks), then round-trip and tamper tests. RFC 8009's PRF vectors are not used: no PRF
  is built (nothing in ADR-0057 needs one).
- Choice: `AesCiphertextStealing` refuses input shorter than one block (`ArgumentException`)
  rather than padding it: Kerberos always encrypts a 16-byte confounder first, so such input never
  occurs, and RFC 3962 leaves the padding unspecified. `TryDecrypt` checks the length first, so a
  short cipher text is a `false`, never an exception.
- Choice: only the default cipher state (all-zero IV) is supported; RFC 4120 and RFC 4121 use no
  other. `KerberosEncryptionProfileTests.KerberosEncryptionType_IsTheLibrarysOnlyPublicType` pins
  today's public surface; BL-239 widens it when it adds the keytab and acceptor types.
- Measured: `Measure-CodeQuality.ps1 -Library Surl.Kerberos.UnitLibrary`: 100% line, 100% branch,
  28 members, 0 failing, worst CRAP 5. No upstream curl measurement: nothing here is on the wire.

## Log

- 2026-09-30: Created.
- 2026-09-30: Filed by BL-217 (ADR-0057 decision 12).
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Kerberos encrypts, decrypts and checksums with enctypes 17-20, pinned by the RFC 3961, 3962 and 8009 vectors
