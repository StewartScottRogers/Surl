---
id: BL-259
title: Record in an ADR that libssh2 1.11.1 on WinCNG fails 1 SSH key exchange in 256 against a correct server
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-039
created: 2026-09-30
completed:
---
# BL-259 — Record in an ADR that libssh2 1.11.1 on WinCNG fails 1 SSH key exchange in 256 against a correct server

## Goal

An ADR records that the pinned Windows reference curl build (libssh2 1.11.1 on WinCNG) fails
about 1 SSH key exchange in 256 against surl because of a libssh2 defect, and that surl stays
RFC-correct rather than working around it.

## Context

- BL-251 found the cause. See its Notes for the measurement: 2000 sftp downloads by the pinned
  Windows reference build (SHA-256 `0E773709...`) against surl, 9 failures
  (`curl: (2) Failure establishing ssh session: -8, Unable to exchange encryption keys`), and
  in every one of the 9, and in none of the 1991 successes, the shared secret K had a zero top
  byte (511 bytes in the 4096-bit group instead of 512).
- surl hashes K as an `mpint` in the fewest bytes, as RFC 4251 section 5 and RFC 4253
  sections 7.2 and 8 require. libssh2's WinCNG backend keeps the fixed-width buffer
  `BCryptDeriveKey(BCRYPT_KDF_RAW_SECRET)` returns, so it hashes K with the extra zero
  byte, gets a different H, and rejects the host key signature. The fix is in libssh2 PR
  #2583 (https://github.com/libssh2/libssh2/pull/2583, merged 2026-09-08, after 1.11.1;
  long-running issue https://github.com/libssh2/libssh2/issues/804), which also normalizes
  the client's e.
- Decisions to record: surl does not redraw its private exponent until K has a non-zero top
  byte. That would hide the client's defect and make the exchange's timing depend on the
  secret. Integration tests that run the Windows reference pin over scp or sftp may fail about
  1 connection in 256 until the pinned build carries a libssh2 with PR #2583. Pinning a newer
  build is Stewart's call (downloading an upstream curl build needs his approval).
- Never cite the Curl port as evidence (ADR-0003).

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions`, marked "Decided by Claude under
      Stewart's delegation", states the cause, the BL-251 measurement, the libssh2 PR and
      issue, and the decision not to work around the defect in surl.
- [ ] The ADR names the flake rate the scp and sftp Integration tests in
      `Surl.Conformance.UnitTests` should expect on the Windows reference pin, and what clears it.

## Notes

## Log

- 2026-09-30: Created.
