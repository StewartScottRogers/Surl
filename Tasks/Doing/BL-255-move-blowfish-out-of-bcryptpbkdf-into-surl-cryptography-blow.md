---
id: BL-255
title: Move Blowfish out of BcryptPbkdf into Surl.Cryptography.Blowfish
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-254]
touches: [Surl.Cryptography.Blowfish.UnitLibrary, Surl.Cryptography.Blowfish.UnitTests, Surl.Cryptography.BcryptPbkdf.UnitLibrary, Surl.Cryptography.BcryptPbkdf.UnitTests]
requirement: FR-039
created: 2026-09-30
completed:
---
# BL-255 — Move Blowfish out of BcryptPbkdf into Surl.Cryptography.Blowfish

## Goal

`Surl.Cryptography.Blowfish.UnitLibrary` holds a public Blowfish block cipher, with the standard
key schedule, bcrypt's salted (eksblowfish) key schedule and 8-byte block encrypt/decrypt;
`Surl.Cryptography.BcryptPbkdf.UnitLibrary` uses it instead of its own internal copy, so
`Surl.Protocol.Ssh` can build `blowfish-cbc` on it (BL-258).

## Context

- ADR-0061 (`Documentation/Planning/Decisions/ADR-0061-blowfish-cast-128-and-ripemd-160-for-curls-openssl-builds.md`)
  amends ADR-0051 decision 2: `blowfish-cbc` is offered only with
  `--allow-weak-ssh-algorithms`, after `arcfour` in decision 2's weak cipher list. It decides
  the Blowfish that BL-220 built inside BcryptPbkdf moves to its own library and is made
  public; BcryptPbkdf then references Blowfish, which is the only reference either library has
  (ADR-0002 decision 3's table as ADR-0061 amends it; `ProtocolIsolationTests` already carries
  `[BcryptPbkdf] = [Blowfish]` from BL-254).
- What moves, from `Surl.Cryptography.BcryptPbkdf.UnitLibrary`:
  - `BlowfishState.cs` (internal `BlowfishState`: `Initialize`, `ExpandKey(key)` =
    `Blowfish_expand0state`, `ExpandKey(data, key)` = `Blowfish_expandstate`,
    `Encrypt(ref uint, ref uint)`, `Decrypt(ref uint, ref uint)`, `Clear`, `ReadWord`), from
    OpenBSD's `blf.c`.
  - `BlowfishPiDigits.cs` (initial P-array and S-boxes).
  - `Surl.Cryptography.BcryptPbkdf.UnitTests/BlowfishStateTests.cs` (Eric Young ECB vectors,
    pi digits, `ReadWord`, round trip).
  `BcryptPbkdf.cs` uses `Initialize`, both `ExpandKey` overloads, `Encrypt` on word pairs,
  `Clear` and `ReadWord`; the public surface must give it everything it uses today, since
  bcrypt passes 64-byte SHA-512 digests as the key and salt, lengths the standard schedule
  streams cyclically.
- Public surface: name the type for what it is (`Blowfish` in namespace
  `Surl.Cryptography.Blowfish`, or the name the glossary already uses), with at least:
  constructing or keying from a key with the standard schedule; the salted schedule bcrypt
  needs; `EncryptBlock`/`DecryptBlock` over one 8-byte block (big-endian halves, as Blowfish is
  specified and as `vectors-2.txt` lays out the bytes); zeroing the state. CBC chaining is not
  this library's: it stays in `Surl.Protocol.Ssh` (BL-258). Keep the word-pair operations
  bcrypt uses reachable, either public or through the block methods.
- Test vectors, each cited beside its test: Eric Young's Blowfish vectors, published by
  Schneier as `vectors-2.txt` (https://www.schneier.com/wp-content/uploads/2015/12/vectors-2.txt):
  the 34 ECB vectors (8-byte keys) and the "set_key" vectors, which cover key lengths 1 to 24
  bytes; use every one of those from 4 bytes up, including the 16-byte key `blowfish-cbc` uses
  (RFC 4253 section 6.3: `blowfish-cbc` has a 128-bit key). Blowfish accepts keys of 4 to 56
  bytes (Schneier, 1993); for 25 to 56 bytes, where the file has no vector, test that a 56-byte
  key encrypts and decrypts a block back to itself and gives a different ciphertext from its
  24-byte prefix. The file's CBC vector (16-byte key) may be checked by chaining single blocks
  in the test.
- Code may be copied from the Curl port; no expected value comes from it (ADR-0003). No package.

## Acceptance criteria

- [ ] `Surl.Cryptography.Blowfish.UnitLibrary` holds the public Blowfish type and its pi-digit
      tables; `Surl.Cryptography.BcryptPbkdf.UnitLibrary` no longer holds `BlowfishState.cs` or
      `BlowfishPiDigits.cs`, and its csproj's only `ProjectReference` is
      `Surl.Cryptography.Blowfish.UnitLibrary`. The Blowfish library references nothing.
- [ ] `Surl.Cryptography.Blowfish.UnitTests` has tests, each citing `vectors-2.txt` beside it,
      that pass for: all 34 ECB vectors, encrypt and decrypt; the set_key vectors for key lengths
      4 to 24 bytes, including 16; the 56-byte key round trip; and the salted schedule and
      `ReadWord` behaviour now in `BlowfishStateTests.cs` (moved, not duplicated).
- [ ] Every test in `Surl.Cryptography.BcryptPbkdf.UnitTests/BcryptPbkdfTests.cs` passes unchanged
      in its expected values.
- [ ] `Surl.Cryptography.Blowfish.UnitLibrary/CLAUDE.md` and
      `Surl.Cryptography.BcryptPbkdf.UnitLibrary/CLAUDE.md` state what each holds after the move
      and that BcryptPbkdf references Blowfish (ADR-0061).
- [ ] `dotnet build Surl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; no test needs `TestCategory=Integration`.
- [ ] Quality gates for both libraries: 100% line and 100% branch coverage, cyclomatic
      complexity at most 10 per method (`CA1502`), CRAP at most 30
      (`powershell -NoProfile -File Measure-CodeQuality.ps1`).
- [ ] Tests are platform-neutral: no path, error text or behaviour that differs on Linux or macOS.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
