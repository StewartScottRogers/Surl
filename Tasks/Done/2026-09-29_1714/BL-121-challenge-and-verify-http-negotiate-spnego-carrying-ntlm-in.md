---
id: BL-121
title: Challenge and verify HTTP Negotiate (SPNEGO carrying NTLM) in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-120]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Conformance.UnitTests, Documentation/Planning/Decisions]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-121 — Challenge and verify HTTP Negotiate (SPNEGO carrying NTLM) in Surl.Authentication

## Goal

`Surl.Authentication` answers HTTP Negotiate (RFC 4559, SPNEGO RFC 4178) carrying the NTLM
mechanism, so `curl --negotiate -u name:password` from the Windows reference build (SSPI)
logs in to `surl`, with Kerberos inside Negotiate left to later work as ADR-0032 decision 11
records.

## Context

FR-014; ADR-0032 (BL-100) decisions 3, 4, 6 and 11. Built on BL-120 (NTLM, including the
multi-response `Record-CurlExchange.ps1` mode it added).

- SPNEGO tokens are DER (ASN.1): `System.Formats.Asn1` is in the shared framework
  (`AsnReader`/`AsnWriter`), so no package is needed. The server answers `NegTokenInit`
  offering Kerberos and NTLM by selecting NTLM (`negState accept-incomplete`, `supportedMech`
  NTLMSSP OID 1.3.6.1.4.1.311.2.2.10) and wraps BL-120's `CHALLENGE_MESSAGE`; the final
  `negState accept-completed` and `mechListMIC` follow RFC 4178.
- Only the Windows reference build lists `SPNEGO` (`UpstreamCurlBuilds.json`: the Linux and
  macOS builds have no `SPNEGO` or `Kerberos`), so the conformance test is
  `[OSCondition(OperatingSystems.Windows)]`. Measure first with the pinned Windows build and
  the multi-response recorder: `-sS --negotiate -u tester:secret http://127.0.0.1:P/x`, and
  record what SSPI sends when the server offers only `WWW-Authenticate: Negotiate` (it may
  try Kerberos first; record it). Save fixtures with a `README.md`.

## Acceptance criteria

- [x] Tests replay the measured SPNEGO tokens, pin the server's `NegTokenResp` bytes for a
      fixed NTLM challenge, and verify the final token; a token offering no NTLM mechanism
      gets the refusal ADR-0032 decides; a malformed DER token is a refusal, never an
      exception. (The pinned build sent no SPNEGO token here, which is measured and pinned in
      `negotiate-no-token`; the tests replay the NTLM messages it sent for BL-120, bare and
      wrapped in SPNEGO. See Notes.)
- [x] Moved to BL-134 (see Notes): a Windows-only `[TestCategory("Integration")]` conformance
      test proves `curl -sS --negotiate -u tester:secret http://.../file` exits 0 with the
      file's bytes, and a wrong password gets the measured exit code.
- [x] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests
      pass on Windows, Linux and macOS; 100% line and branch coverage kept.
- [x] `Surl.Authentication.UnitLibrary/CLAUDE.md` names Negotiate (carrying NTLM) among the
      methods it holds, and that Kerberos inside Negotiate is later work.

## Notes

- **Measured (2026-09-29).** Offered `WWW-Authenticate: Negotiate`, the pinned Windows build
  (curl 8.21.0, SSPI) sends **no token** on the lane machine: `InitializeSecurityContext failed:
  SEC_E_NO_CREDENTIALS`, exit 0 (22 with `-f`), for `127.0.0.1`, `localhost`, a `--resolve`d
  name, the computer name, `SURL\tester`, `tester@surl`, `.\tester`, `-u :` and
  `--delegation always`. A C# probe making curl's exact SSPI calls (read from curl 8.21.0's
  `lib/vauth/spnego_sspi.c`) on the same machine gets a bare NTLM `NEGOTIATE_MESSAGE`, so the
  cause is specific to curl's process and still unknown. Fixture `negotiate-no-token`.
- **Criterion 2 moved to BL-134.** Two reasons: the Windows build sends nothing to prove
  against here, and `surl` composes HTTP methods in `Surl.Console` (`AuthenticationComposition`),
  which this task's `touches` do not name and BL-132 is set to change for NTLM. This is the same
  split BL-120 made into BL-132. BL-134 (depends on BL-121 and BL-132) composes Negotiate, finds
  the cause of `SEC_E_NO_CREDENTIALS`, records the real handshake and writes the conformance test.
  `Surl.Conformance.UnitTests` was therefore not changed.
- **Decisions** (ADR-0040, decided by Claude under Stewart's delegation): a bare NTLM token
  inside Negotiate is answered bare (what Windows' Negotiate package itself produced in the
  probe); SPNEGO selects NTLMSSP only, answering the optimistic token when NTLM is first and
  otherwise naming NTLM alone; the final token is `negTokenResp { accept-completed }`; no
  `mechListMIC` either way, because ADR-0039's `CHALLENGE_MESSAGE` grants no signing, so the
  context has no integrity (RFC 4178 section 5); no NTLM offered, a bare Kerberos token and
  malformed DER are refused.
- **Refactor.** The NTLM handshake moved out of `NtlmConnectionVerifier` into `NtlmHandshake`
  (with `NtlmHandshakeStep` and `Base64Credentials`) so NTLM and Negotiate share it; the NTLM
  tests are unchanged and pass.
- **Touches.** Added `Documentation/Planning/Decisions` for ADR-0040 and the index row; no task
  in `Doing` names it.
- **Quality.** `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary`: 100% line,
  100% branch, highest cyclomatic complexity 10. `dotnet format --verify-no-changes` is clean
  for both Authentication projects. It does report end-of-line errors in
  `Surl.Console/LogStreams.cs`, which predate this task and sit outside its `touches`.
- The `negTokenResp` bytes the tests pin were written out by hand from RFC 4178's DER
  (`A16D306B A0030A0101 A10C060A2B06010401823702020A A2560454 <84-byte CHALLENGE_MESSAGE>`,
  final `A1073005A0030A0100`), not produced by the code under test.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.Authentication answers HTTP Negotiate carrying NTLM, bare or in SPNEGO (ADR-0040); the end-to-end proof is BL-134
