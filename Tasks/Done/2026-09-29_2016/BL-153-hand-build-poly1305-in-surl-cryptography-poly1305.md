---
id: BL-153
title: Hand-build Poly1305 in Surl.Cryptography.Poly1305
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-149]
touches: [Surl.Cryptography.Poly1305.UnitLibrary, Surl.Cryptography.Poly1305.UnitTests]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-29
---
# BL-153 — Hand-build Poly1305 in Surl.Cryptography.Poly1305

## Goal

`Surl.Cryptography.Poly1305` computes the Poly1305 tag of a message under a 32-byte one-time
key (RFC 8439 section 2.5), so BL-169 can authenticate `chacha20-poly1305@openssh.com`
packets.

## Context

- Decision: BL-148's ADR (this library references nothing).
- Shape: a static `Poly1305.ComputeTag(key, message)` returning 16 bytes, or writing into a
  destination span. SSH authenticates the encrypted length and the ciphertext as one message,
  so one-shot suffices; add an incremental form only if BL-169's plan shows it avoids a copy.
- Clamping of r as section 2.5.1 states; arithmetic mod 2^130-5 without secret-dependent
  branches. Comparing a received tag is the caller's job with
  `CryptographicOperations.FixedTimeEquals`; say so in the XML doc.
- The Curl port's `Poly1305.cs` may be copied as code; expected values come from the RFC only
  (ADR-0003).

## Acceptance criteria

- [x] Tests pass RFC 8439 section 2.5.2's example and every Appendix A.3 test vector (#1 to
      #11), each expected tag copied from the RFC and cited beside it.
- [x] `dotnet build Surl.Cryptography.Poly1305.UnitLibrary -warnaserror` is clean; the fast
      tests pass; `Measure-CodeQuality.ps1 -Library Surl.Cryptography.Poly1305.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- Copied the Curl port's `Poly1305.cs` as code (ADR-0003 allows it) into namespace `Surl.Cryptography.Poly1305`, trimmed to what this task asks: `ComputeTag(key, message)` returning 16 bytes and `ComputeTag(key, message, tag)` writing into a span. Dropped the port's `Verify` (the task makes comparison the caller's job, stated in the XML doc), its AEAD zero-padding mode and its incremental internals (SSH authenticates one message; BL-169 can add an incremental form if its plan needs one), and inlined `ConstantTime.Select` as a private `SelectByMask` because this library references nothing (ADR-0048).
- Expected tags are the RFC's own (section 2.5.2, Appendix A.3 #1 to #11), one test per vector with its citation; 19 tests.
- `Measure-CodeQuality.ps1 -Library Surl.Cryptography.Poly1305.UnitLibrary`: 100% line, 100% branch, 9 members, 0 failing, worst CRAP 4.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Poly1305.ComputeTag passes RFC 8439 section 2.5.2 and Appendix A.3 #1-#11 at 100% line and branch coverage
