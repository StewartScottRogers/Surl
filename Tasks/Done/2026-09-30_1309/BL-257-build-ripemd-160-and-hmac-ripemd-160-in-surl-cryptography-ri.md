---
id: BL-257
title: Build RIPEMD-160 and HMAC-RIPEMD-160 in Surl.Cryptography.Ripemd160
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-254]
touches: [Surl.Cryptography.Ripemd160.UnitLibrary, Surl.Cryptography.Ripemd160.UnitTests]
requirement: FR-039
created: 2026-09-30
completed: 2026-09-30
---
# BL-257 — Build RIPEMD-160 and HMAC-RIPEMD-160 in Surl.Cryptography.Ripemd160

## Goal

`Surl.Cryptography.Ripemd160.UnitLibrary` holds public RIPEMD-160 (20-byte digest) and
HMAC-RIPEMD-160 (RFC 2286), so `Surl.Protocol.Ssh` can offer `hmac-ripemd160` and
`hmac-ripemd160@openssh.com` on it (BL-258).

## Context

- ADR-0061 (`Documentation/Planning/Decisions/ADR-0061-blowfish-cast-128-and-ripemd-160-for-curls-openssl-builds.md`)
  amends ADR-0051 decision 2: `hmac-ripemd160` and `hmac-ripemd160@openssh.com`, offered by
  upstream curl 8.21.0's OpenSSL builds (pinned by
  `Surl.Conformance.UnitTests/UpstreamCurlOffersSshAlgorithmsTests.cs`), are offered only with
  `--allow-weak-ssh-algorithms`, after `hmac-md5-96` in decision 2's weak MAC list. .NET 10's
  BCL has no RIPEMD-160 (`HashAlgorithmName` has none, and `RIPEMD160` is .NET Framework only),
  so it is hand-built here; the library references nothing (ADR-0061's row in ADR-0002
  decision 3's table).
- Specifications:
  - RIPEMD-160: Dobbertin, Bosselaers, Preneel, "RIPEMD-160: A Strengthened Version of RIPEMD"
    (1996), and the authors' page https://homes.esat.kuleuven.be/~bosselae/ripemd160.html, which
    publishes the reference vectors.
  - HMAC-RIPEMD-160: RFC 2286 (https://www.rfc-editor.org/rfc/rfc2286), which applies RFC 2104's
    HMAC with a 64-byte block; a key longer than 64 bytes is hashed first.
- API: a one-shot `Hash(ReadOnlySpan<byte>)` (or into a destination span) and an HMAC over a key
  and a message span, each giving 20 bytes; `Surl.Protocol.Ssh`'s `SshHmac.Tag` takes the key as
  `byte[]` and the message as `ReadOnlySpan<byte>`. Name types for what they are
  (`Ripemd160`, `HmacRipemd160`).
- Code may be copied from the Curl port; no expected value comes from it (ADR-0003). No package.

## Acceptance criteria

- [x] `Surl.Cryptography.Ripemd160.UnitLibrary` holds the public RIPEMD-160 and HMAC-RIPEMD-160
      types and references nothing.
- [x] `Surl.Cryptography.Ripemd160.UnitTests` has tests, each citing the authors' published list
      beside it, that pass for every message in it: `""`, `"a"`, `"abc"`, `"message digest"`,
      `"abcdefghijklmnopqrstuvwxyz"`,
      `"abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq"`,
      `"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"`, eight repetitions of
      `"1234567890"`, and 1,000,000 repetitions of `"a"` - each giving the listed digest.
- [x] Tests, each citing RFC 2286 section 2, pass for all seven HMAC-RIPEMD-160 test cases there,
      including the ones with an 80-byte key (hashed first).
- [x] `Surl.Cryptography.Ripemd160.UnitLibrary/CLAUDE.md` states what the library now holds.
- [x] `dotnet build Surl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; no test needs `TestCategory=Integration`.
- [x] Quality gates: 100% line and 100% branch coverage, cyclomatic complexity at most 10 per
      method (`CA1502`), CRAP at most 30 (`powershell -NoProfile -File Measure-CodeQuality.ps1`).
- [x] Tests are platform-neutral.

## Notes

- The project scaffold already existed. RIPEMD-160 and HMAC-RIPEMD-160 were copied from the
  Curl port's `Curl.Cryptography` (code only, ADR-0003) and adapted. Every expected value is
  from the authors' published list or RFC 2286 section 2, cited beside each test.
- Choice: the port's shared `LittleEndianMerkleDamgard`/`ILittleEndianCompressionFunction` is
  folded into `Ripemd160` itself. This library has only one hash, so a generic padding class
  would have only one user. `Ripemd160.BlockSize` is public so `HmacRipemd160` can use it.
- Choice: the API mirrors the BCL's hash statics: `HashData(source, destination)` and
  `HashData(source)` returning `byte[]`; `HmacRipemd160.HashData(key, source[, destination])`
  and a fixed-time `Verify`. A `byte[]` key converts implicitly to `ReadOnlySpan<byte>`, so
  `SshHmac.Tag` (BL-258) can call `HmacRipemd160.HashData(key, message)` directly.
- The 1,000,000 x "a" vector runs in well under a second, so it stays a fast test.
- Measured: 100% line, 100% branch, worst CRAP 6 (`Measure-CodeQuality.ps1`); 38 tests.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Cryptography.Ripemd160 holds RIPEMD-160 and HMAC-RIPEMD-160, pinned to the authors' vectors and RFC 2286
