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
completed:
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

- [ ] `Surl.Cryptography.Ripemd160.UnitLibrary` holds the public RIPEMD-160 and HMAC-RIPEMD-160
      types and references nothing.
- [ ] `Surl.Cryptography.Ripemd160.UnitTests` has tests, each citing the authors' published list
      beside it, that pass for every message in it: `""`, `"a"`, `"abc"`, `"message digest"`,
      `"abcdefghijklmnopqrstuvwxyz"`,
      `"abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq"`,
      `"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"`, eight repetitions of
      `"1234567890"`, and 1,000,000 repetitions of `"a"` - each giving the listed digest.
- [ ] Tests, each citing RFC 2286 section 2, pass for all seven HMAC-RIPEMD-160 test cases there,
      including the ones with an 80-byte key (hashed first).
- [ ] `Surl.Cryptography.Ripemd160.UnitLibrary/CLAUDE.md` states what the library now holds.
- [ ] `dotnet build Surl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; no test needs `TestCategory=Integration`.
- [ ] Quality gates: 100% line and 100% branch coverage, cyclomatic complexity at most 10 per
      method (`CA1502`), CRAP at most 30 (`powershell -NoProfile -File Measure-CodeQuality.ps1`).
- [ ] Tests are platform-neutral.

## Notes

## Log

- 2026-09-30: Created.
