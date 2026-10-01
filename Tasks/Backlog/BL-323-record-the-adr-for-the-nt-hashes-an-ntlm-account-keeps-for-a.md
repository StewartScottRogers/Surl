---
id: BL-323
title: Record the ADR for the NT hashes an NTLM account keeps for a non-ASCII password
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-014
created: 2026-09-30
completed:
---
# BL-323 — Record the ADR for the NT hashes an NTLM account keeps for a non-ASCII password

## Goal

An ADR, "Decided by Claude under Stewart's delegation", records the decision BL-321 made and
implemented: every NTLM account keeps four NT hashes and accepts an answer proving any of them.

## Context

- BL-321 (2026-09-30) could not write the ADR itself: `Documentation/Planning/Decisions` was held
  by BL-285 in another lane. The decision is already in the code
  (`Surl.Authentication.UnitLibrary/NtlmPasswordHashes.cs`, `NtlmHandshake.FindAnsweringAccount`)
  and the measurements in `Surl.Authentication.UnitTests/Fixtures/README.md`, "Non-ASCII NTLM
  password (BL-321)".
- Measured with `pässword` against the fixed challenge `0123456789abcdef`:
  - stunnel/static-curl 8.21.0 for Windows (Unicode feature, SSPI): `MD4(UTF-16LE(password))`,
    from the command line and from a UTF-8 `-K` file alike.
  - The Windows reference build (Git for Windows, Schannel, SSPI, no Unicode feature): SSPI gets
    an ANSI identity and reads its bytes in the OEM code page, so the hash is
    `MD4(UTF-16LE(cp437(cp1252(password))))` from the command line and
    `MD4(UTF-16LE(cp437(UTF-8(password))))` from a `-K` file, on a machine with ANSI code page
    1252 and OEM code page 437.
  - Upstream curl's own NTLM code (Linux and macOS builds) widens each UTF-8 byte
    (`lib/curl_ntlm_core.c` at `curl-8_21_0`); recording it is BL-322.
- What the ADR must say: the four hashes and which build each serves; that the code pages 1252
  and 437 are fixed (US-English Windows, the machine the reference build was measured on) and a
  client with other code pages is not matched; that each extra hash is a deterministic form of
  the same password, so it admits only a near-guess of it (e.g. `pΣssword` for an account
  `pässword`), which was judged acceptable; that every hash is checked whatever matches and the
  dummy account keeps four random hashes, so the work stays constant (ADR-0032, section 8); that
  the encodings come from `CodePagesEncodingProvider.Instance`, in the shared framework (no
  package); and that it amends ADR-0039.

## Acceptance criteria

- [ ] An ADR in `Documentation/Planning/Decisions` states each point above, marked "Decided by
      Claude under Stewart's delegation", with its row in the folder's `README.md`.

## Notes

## Log

- 2026-09-30: Created.
