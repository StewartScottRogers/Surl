---
id: BL-120
title: Challenge and verify HTTP NTLM in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-118, BL-119]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Record-CurlExchange.ps1, Surl.Conformance.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
---
# BL-120 — Challenge and verify HTTP NTLM in Surl.Authentication

## Goal

`Surl.Authentication` answers HTTP NTLM ([MS-NLMP]) as ADR-0032 decides: it replies to
upstream curl's `NEGOTIATE_MESSAGE` with a `CHALLENGE_MESSAGE` in a `401` and verifies the
`AUTHENTICATE_MESSAGE` against the account's password on the same connection, so
`curl --ntlm -u name:password` logs in to `surl` and a wrong password is refused.

## Context

FR-014; ADR-0032 (BL-100) decisions 3, 4 and 6 (NTLM is not a plain-text secret; the
connection-bound state the contract keeps) and 7 (`Surl.Authentication` references
`Surl.Cryptography`). Built on BL-119 (MD4), BL-110 (accounts, policy) and BL-118 (the
composed server and conformance harness, so this task adds a method in
`Surl.Authentication` and a conformance test, and changes neither the HTTP server nor
`Surl.Console` - ADR-0032 decision 6 makes that possible; if it turns out not to be, block
and have the planner file the missing contract work).

- HMAC-MD5 is in the BCL (`HMACMD5.HashData`); NTLMv2 per [MS-NLMP] section 3.3.2; NTLMv1
  only if the measurement shows a pinned build sends it.
- NTLM needs several round trips on one connection. `Record-CurlExchange.ps1` closes each
  connection after one response, so extend it (a documented parameter, e.g. several
  responses on one connection) to measure: `-sS --ntlm -u tester:secret http://127.0.0.1:P/x`
  with the Windows reference build (SSPI) and, on CI or by stating it is measured only in the
  conformance test, the Linux build (curl's own NTLM). Save `request.bin` of each leg as a
  fixture with a `README.md` (command line, build SHA-256, date).
- The server's challenge must be random (`RandomNumberGenerator`) in production and fixed by
  an injected source in tests, so recorded curl answers can be verified.

## Acceptance criteria

- [ ] Tests replay the measured `NEGOTIATE_MESSAGE`, pin the `CHALLENGE_MESSAGE` bytes the
      server sends for a fixed challenge and target info, and verify the measured
      `AUTHENTICATE_MESSAGE` for the account; a changed NT proof, user, domain or a replayed
      message on a new connection is refused.
- [ ] Tests reproduce [MS-NLMP] section 4.2.4's NTLMv2 example values (NTOWFv2, NTProofStr,
      session base key) from the specification's inputs.
- [ ] `Record-CurlExchange.ps1`'s new parameter has a `.PARAMETER` block and an
      `.EXAMPLE`, and the script's existing modes behave as before.
- [ ] An `[TestCategory("Integration")]` conformance test proves
      `curl -sS --ntlm -u tester:secret http://.../file` exits 0 with the file's bytes and a
      wrong password gets the measured exit code, on every platform whose pinned build lists
      `NTLM`.
- [ ] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests
      pass; 100% line and branch coverage kept; no method exceeds complexity 10.
- [ ] `Surl.Authentication.UnitLibrary/CLAUDE.md` names NTLM among the methods it holds.

## Notes

## Log

- 2026-09-29: Created.
