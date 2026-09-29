---
id: BL-121
title: Challenge and verify HTTP Negotiate (SPNEGO carrying NTLM) in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-120]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Conformance.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
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

- [ ] Tests replay the measured SPNEGO tokens, pin the server's `NegTokenResp` bytes for a
      fixed NTLM challenge, and verify the final token; a token offering no NTLM mechanism
      gets the refusal ADR-0032 decides; a malformed DER token is a refusal, never an
      exception.
- [ ] A Windows-only `[TestCategory("Integration")]` conformance test proves
      `curl -sS --negotiate -u tester:secret http://.../file` exits 0 with the file's bytes,
      and a wrong password gets the measured exit code.
- [ ] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests
      pass on Windows, Linux and macOS; 100% line and branch coverage kept.
- [ ] `Surl.Authentication.UnitLibrary/CLAUDE.md` names Negotiate (carrying NTLM) among the
      methods it holds, and that Kerberos inside Negotiate is later work.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
