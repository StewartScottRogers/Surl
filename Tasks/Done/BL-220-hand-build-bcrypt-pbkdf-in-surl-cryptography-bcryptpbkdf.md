---
id: BL-220
title: Hand-build bcrypt_pbkdf in Surl.Cryptography.BcryptPbkdf
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Cryptography.BcryptPbkdf.UnitLibrary, Surl.Cryptography.BcryptPbkdf.UnitTests, Surl.slnx, Surl.Protocol.Abstractions.UnitTests]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-29
---
# BL-220 — Hand-build bcrypt_pbkdf in Surl.Cryptography.BcryptPbkdf

## Goal

`Surl.Cryptography.BcryptPbkdf.UnitLibrary` exists beside its `.UnitTests` twin, is listed in
`Surl.slnx` and guarded by `ProtocolIsolationTests`, and derives key bytes exactly as OpenSSH's
`bcrypt_pbkdf` does, so BL-223 can decrypt `openssh-key-v1` private keys written with a
passphrase.

## Context

- Decision: ADR-0051 (BL-154) - it adds `Surl.Cryptography.BcryptPbkdf.UnitLibrary` to ADR-0002
  decision 3's table of horizontal libraries, referencing nothing. The BCL has neither Blowfish
  nor bcrypt_pbkdf, so both are hand-built (root `CLAUDE.md`, "Decisions").
- Creating the projects: follow `.claude/skills/new-project/SKILL.md` and copy what BL-149
  (`Tasks/Done/BL-149-create-the-curve25519-ed25519-chacha20-and-poly1305-primitiv.md`) did for
  the four SSH primitive libraries:
  - csproj shapes from `Surl.Cryptography.ChaCha20.UnitLibrary` and
    `Surl.Cryptography.ChaCha20.UnitTests`;
  - `Surl.slnx`: the two `<Project>` lines in ordinal order in the flat run, library then
    `.UnitTests`, directly before `Surl.Cryptography.ChaCha20.UnitLibrary`, no solution folder;
  - `Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`: a `HorizontalLibraries`
    row `[BcryptPbkdf] = []` and the `ForbiddenProtocolReferences_AllowedLibrary_IsNotForbidden`
    data row for it;
  - `Surl.Cryptography.BcryptPbkdf.UnitLibrary/CLAUDE.md` in the shape of
    `Surl.Cryptography.ChaCha20.UnitLibrary/CLAUDE.md`: what it holds, that it references
    nothing, bytes in and bytes out, vectors cited beside each test, copying from the Curl port
    allowed as code only (ADR-0003).
- Specification: OpenSSH's `openbsd-compat/bcrypt_pbkdf.c` and `openbsd-compat/blowfish.c`
  (openssh-portable, tag `V_9_9_P1`, the tag ADR-0048 cites). The algorithm: SHA-512 of the
  passphrase; for each 32-byte output block, SHA-512 of salt followed by the big-endian 32-bit
  block count; the inner `bcrypt_hash` runs the expensive Blowfish key schedule
  ("eksblowfish": 64 rounds alternately expanding with the salt hash and the passphrase hash)
  and then encrypts `"OxychromaticBlowfishSwatDynamite"` 64 times; `rounds - 1` further
  iterations are XORed in; the output bytes are spread across the blocks by the stride
  (`key[i * stride + block]`). SHA-512 is the BCL's `SHA512`; Blowfish (Schneier, 1993) is an
  internal type of this library.
- Shape: a static `BcryptPbkdf` with one derivation method (passphrase bytes, salt bytes,
  rounds, output span). Rounds below 1, an empty salt, an empty output or an output longer
  than 1024 bytes are refused with an argument exception, as the C function refuses them.
- The Curl port's code may be copied; expected values come from published sources only
  (ADR-0003).

## Acceptance criteria

- [x] `Surl.Cryptography.BcryptPbkdf.UnitLibrary` and `Surl.Cryptography.BcryptPbkdf.UnitTests`
      exist at the repository root; the production csproj has no `PackageReference` and no
      `ProjectReference`; `Surl.slnx` lists both in the position given in Context.
- [x] `Surl.Cryptography.BcryptPbkdf.UnitLibrary/CLAUDE.md` exists with the content listed in
      Context.
- [x] `ProtocolIsolationTests` has the `BcryptPbkdf` row and passes, including
      `EveryHorizontalLibrary_ReferencesOnlyItsRow`.
- [x] The internal Blowfish passes at least three rows of Eric Young's published Blowfish ECB
      test vectors (key, plaintext, ciphertext), the source cited beside them.
- [x] `BcryptPbkdf` reproduces at least three published bcrypt_pbkdf vectors with different
      rounds and output lengths (for example OpenBSD's `regress/lib/libutil/bcrypt_pbkdf`
      vectors), each expected value copied from its source and cited beside it.
- [x] Tests show each refusal named in Context.
- [x] `dotnet build Surl.Cryptography.BcryptPbkdf.UnitLibrary -warnaserror` is clean;
      `dotnet build` and `dotnet test --filter "TestCategory!=Integration"` are green;
      `Measure-CodeQuality.ps1 -Library Surl.Cryptography.BcryptPbkdf.UnitLibrary` reports 100%
      line and 100% branch coverage, no method above cyclomatic complexity 10, and no failing
      member.

## Notes

- Code copied from the Curl port (`Curl.Cryptography.UnitLibrary`: `BcryptPbkdf`,
  `BlowfishState`, `BlowfishPiDigits`, and their tests), allowed as code only (ADR-0003). Its
  public `Blowfish` class (ECB and CBC) was not copied: bcrypt_pbkdf needs only the internal
  `BlowfishState`, so the Eric Young ECB vectors (nine rows) drive `BlowfishState` directly.
- Every expected value was re-fetched from its published source on 2026-09-29 and compared:
  Schneier's `vectors-2.txt` ECB table, and Go's
  `golang.org/x/crypto/ssh/internal/bcrypt_pbkdf/bcrypt_pbkdf_test.go` (`golden` 0 to 2: 12,
  3 and 8 rounds, 32, 32 and 88 bytes; `TestBcryptHash`), vectors "generated by the reference
  implementation from OpenBSD".
- Choice: the method is `BcryptPbkdf.DeriveKey(password, salt, rounds, destination)`, and it
  refuses everything the C function refuses, not only the four inputs Context names: an empty
  password and a salt over 1 MiB (`1 << 20`) too, because `bcrypt_pbkdf.c` returns -1 for both
  and a faithful port refuses the same inputs. Rounds below 1 throw
  `ArgumentOutOfRangeException` (an `ArgumentException`), the lengths `ArgumentException`.
- Measure-CodeQuality: 100% line, 100% branch, 16 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.Cryptography.BcryptPbkdf derives OpenSSH bcrypt_pbkdf keys, pinned to Go's OpenBSD-generated vectors and Eric Young's Blowfish ECB vectors
