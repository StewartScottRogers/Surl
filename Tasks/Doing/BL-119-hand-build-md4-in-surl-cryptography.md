---
id: BL-119
title: Hand-build MD4 in Surl.Cryptography
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Cryptography.UnitLibrary, Surl.Cryptography.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
---
# BL-119 — Hand-build MD4 in Surl.Cryptography

## Goal

`Surl.Cryptography` computes MD4 (RFC 1320), which the BCL does not offer, so the NTLM
verifier (BL-120) can derive the NT hash of a password.

## Context

NTLM's NT hash is `MD4(UTF-16LE(password))` ([MS-NLMP] section 3.3.1 and 3.3.2); .NET has no
MD4 on any platform, so it is built by hand in `Surl.Cryptography` (root `CLAUDE.md`,
"Decisions"). The pinned builds list `NTLM` among their features (`UpstreamCurlBuilds.json`).

- `Surl.Cryptography.UnitLibrary` references nothing (ADR-0002); BL-112 may already have
  added `Sha512Slash256` there - follow its file and naming style.
- Shape: a static one-shot `Md4.HashData(ReadOnlySpan<byte>)` returning 16 bytes; MD4 is used
  here only for NTLM, and its XML doc says so (it is broken as a general hash).

## Acceptance criteria

- [ ] Tests pass all seven test vectors of RFC 1320 appendix A.5 (`""`, `"a"`, `"abc"`,
      `"message digest"`, the alphabet, the alphanumerics, and the 80-digit string), each
      expected value copied from the RFC (the 80-digit vector crosses a block boundary), plus
      the NTOWFv1 of `Password` from [MS-NLMP] section 4.2.2.1.2, copied from the
      specification and named beside it.
- [ ] `Surl.Cryptography.UnitLibrary.csproj` still references nothing.
- [ ] `dotnet build Surl.Cryptography.UnitLibrary -warnaserror` is clean; the fast tests
      pass; 100% line and branch coverage; no method exceeds complexity 10.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
