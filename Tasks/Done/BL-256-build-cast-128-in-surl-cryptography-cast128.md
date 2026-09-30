---
id: BL-256
title: Build CAST-128 in Surl.Cryptography.Cast128
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-254]
touches: [Surl.Cryptography.Cast128.UnitLibrary, Surl.Cryptography.Cast128.UnitTests]
requirement: FR-039
created: 2026-09-30
completed: 2026-09-30
---
# BL-256 — Build CAST-128 in Surl.Cryptography.Cast128

## Goal

`Surl.Cryptography.Cast128.UnitLibrary` holds a public CAST-128 block cipher (RFC 2144) that
encrypts and decrypts one 8-byte block under a 40- to 128-bit key, so `Surl.Protocol.Ssh` can
build `cast128-cbc` on it (BL-258).

## Context

- ADR-0061 (`Documentation/Planning/Decisions/ADR-0061-blowfish-cast-128-and-ripemd-160-for-curls-openssl-builds.md`)
  amends ADR-0051 decision 2: `cast128-cbc`, offered by upstream curl 8.21.0's OpenSSL builds
  (libssh2 1.11.1 on OpenSSL 4.0.1, pinned by
  `Surl.Conformance.UnitTests/UpstreamCurlOffersSshAlgorithmsTests.cs`), is offered only with
  `--allow-weak-ssh-algorithms`, after `blowfish-cbc` at the end of decision 2's weak cipher
  list. The BCL has no CAST-128, so it is hand-built here; the library references nothing
  (ADR-0061's row in ADR-0002 decision 3's table).
- RFC 2144 (https://www.rfc-editor.org/rfc/rfc2144):
  - section 2.1-2.4: 12 or 16 rounds of the three round-function types over 32-bit halves, with
    the masking and rotation subkeys from the key schedule (section 2.4) and substitution boxes
    S1-S8 (Appendix A).
  - section 2.5: keys from 40 to 128 bits in 8-bit steps; a key shorter than 128 bits is padded
    with zero bytes on the right; keys of 80 bits or less use 12 rounds, longer keys 16.
  So the key is 5 to 16 bytes; any other length throws `ArgumentException`.
- `cast128-cbc` uses a 128-bit key (RFC 4253 section 6.3), but the library implements the whole
  key range RFC 2144 specifies. CBC chaining is not this library's: it stays in
  `Surl.Protocol.Ssh` (BL-258). Expose single-block encrypt and decrypt over 8-byte spans
  (big-endian, as RFC 2144 lays out plaintext and ciphertext).
- Code may be copied from the Curl port; no expected value comes from it (ADR-0003). No package.

## Acceptance criteria

- [x] `Surl.Cryptography.Cast128.UnitLibrary` holds the public CAST-128 type and its S-boxes, and
      references nothing.
- [x] `Surl.Cryptography.Cast128.UnitTests` has tests, each citing RFC 2144 Appendix B beside it,
      that pass for: B.1's single-plaintext vectors with the 128-bit, 80-bit and 40-bit keys,
      encrypt giving the listed ciphertext and decrypt giving the plaintext back; and B.2's
      maintenance test (1,000,000 iterations giving the listed `a` and `b`). The B.2 test may
      carry `[TestCategory("Integration")]` only if it takes longer than one second on the lane's
      machine; then a fast test must still cover every branch it covers.
- [x] A key of 4 bytes and one of 17 bytes each throw `ArgumentException` (tested); a block span
      other than 8 bytes is rejected the same way (tested).
- [x] `Surl.Cryptography.Cast128.UnitLibrary/CLAUDE.md` states what the library now holds.
- [x] `dotnet build Surl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes.
- [x] Quality gates: 100% line and 100% branch coverage from the fast tests, cyclomatic complexity
      at most 10 per method (`CA1502`), CRAP at most 30
      (`powershell -NoProfile -File Measure-CodeQuality.ps1`).
- [x] Tests are platform-neutral.

## Notes

- Code copied from the Curl port's `Cast128` and `Cast128SubstitutionBoxes` (allowed by
  ADR-0003), minus its CBC methods: chaining is `Surl.Protocol.Ssh`'s (BL-258). Every expected
  value was checked against the RFC 2144 Appendix B text itself, not the port.
- Kept the port's `IDisposable` (zeroes the 32 subkeys) as the sensible default for key
  material; a disposed instance throws `ObjectDisposedException` (tested).
- The B.2 maintenance test takes about 21 s on the lane's machine (Debug), so it carries
  `[TestCategory("Integration")]`; it passes. The fast B.1 tests cover both round counts
  (12 and 16), encrypt and decrypt, and all three round-function types, so the fast tests
  alone give 100% line and branch coverage (Measure-CodeQuality: 13 members, worst CRAP 8).
- Block spans are rejected with `ArgumentException` naming `source` or `destination` for
  lengths 7 and 9 of each.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Cryptography.Cast128 encrypts and decrypts one CAST-128 block under 5- to 16-byte keys, pinned to RFC 2144 Appendix B
