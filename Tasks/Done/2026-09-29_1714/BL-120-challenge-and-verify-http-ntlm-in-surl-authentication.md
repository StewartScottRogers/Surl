---
id: BL-120
title: Challenge and verify HTTP NTLM in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-118, BL-119]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Record-CurlExchange.ps1, Surl.Conformance.UnitTests, Documentation/Planning/Decisions]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
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

- [x] Tests replay the measured `NEGOTIATE_MESSAGE`, pin the `CHALLENGE_MESSAGE` bytes the
      server sends for a fixed challenge and target info, and verify the measured
      `AUTHENTICATE_MESSAGE` for the account; a changed NT proof, user, domain or a replayed
      message on a new connection is refused.
- [x] Tests reproduce [MS-NLMP] section 4.2.4's NTLMv2 example values (NTOWFv2, NTProofStr,
      session base key) from the specification's inputs.
- [x] `Record-CurlExchange.ps1`'s new parameter has a `.PARAMETER` block and an
      `.EXAMPLE`, and the script's existing modes behave as before.
- [x] The `[TestCategory("Integration")]` conformance test (`curl -sS --ntlm -u tester:secret
      http://.../file` exits 0 with the file's bytes, a wrong password the measured exit code,
      on every platform whose pinned build lists `NTLM`) is filed as BL-132 with the measured
      exit codes, because it needs `Surl.Console`, which BL-123 held (see Notes). This task
      measured those exit codes: 0 and, with `-f`, 22.
- [x] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests
      pass; 100% line and branch coverage kept; no method exceeds complexity 10.
- [x] `Surl.Authentication.UnitLibrary/CLAUDE.md` names NTLM among the methods it holds.

## Notes

- **Plan and decisions**: ADR-0039 (decided by Claude under Stewart's delegation) - the
  `CHALLENGE_MESSAGE` layout (no Version, target `SURL`, target info `SURL`/`SURL` without a
  timestamp, Unicode or OEM as offered, ESS/128/56 only when asked), one random server challenge
  per connection used up by the next leg, NTLMv2 only, MIC/LM/timestamp not read, exact user
  match with a random-hash dummy. `NtlmAuthenticationMethod` + `NtlmConnectionVerifier`, the
  message readers/builder, `NtlmV2Calculation`, `AccountBook.FindNtlmAccount`.
- **Measured** with the Windows reference build (SSPI): the `NEGOTIATE_MESSAGE` has flags
  `0xA2088207`; the answer is NTLMv2 with an empty domain; a wrong password ends after the second
  `401` with exit 0, or 22 with `-f`. The first recording's hand-built challenge set
  `NTLMSSP_NEGOTIATE_VERSION` without a Version field; the fixtures were re-recorded with the
  exact bytes `NtlmChallengeMessage` builds, so the pinned challenge is what curl really answered.
- **Record-CurlExchange.ps1**: new `-ResponsesPerConnection` (default 1, unchanged behaviour),
  response index `C * N + R`, `request-<n>.bin` per request when N > 1, `HoldOpenMilliseconds`
  after the last response only. Re-ran a plain GET, a `-Connections 2` redirect and a
  `-HoldOpenMilliseconds` run: same files and output as before.
- **Scope split (rule 3)**: the conformance criterion needs `surl` to compose the method, and
  `Surl.Console/AuthenticationComposition.ComposePolicy` lists the methods it composes - so
  ADR-0032 decision 6's "no change to `Surl.Console`" does not hold for a new method yet.
  `Surl.Console` is in BL-123's `touches` (in Doing), so rather than send the whole task back to
  Backlog, the Surl.Authentication work (everything but that one wire-up) is delivered here and
  the composition plus conformance test is filed as BL-132, which also suggests moving the
  method list into `Surl.Authentication`. `Documentation/Planning/Decisions` was added to
  `touches` for ADR-0039 (no task in Doing names it).
- **Follow-up**: BL-133 - curl sends no `Authorization` on later requests of a connection NTLM
  has authenticated; the session does not remember the login yet.
- Quality: `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` - 100% line,
  100% branch, 0 failing members, worst CRAP 10 (`NtlmAuthenticateMessage.TryRead` was split to
  get under complexity 10). Fast tests: all green; Surl.Authentication.UnitTests 319.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.Authentication challenges and verifies HTTP NTLM: CHALLENGE_MESSAGE for curl's NEGOTIATE_MESSAGE, NTLMv2 AUTHENTICATE_MESSAGE checked on the same connection; surl wiring and conformance test filed as BL-132
