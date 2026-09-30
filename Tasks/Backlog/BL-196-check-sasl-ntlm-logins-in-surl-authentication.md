---
id: BL-196
title: Check SASL NTLM logins in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-195]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-046
created: 2026-09-29
completed:
---
# BL-196 — Check SASL NTLM logins in Surl.Authentication

## Goal

`AuthenticationPolicy` runs the SASL `NTLM` mechanism through BL-193's contract - the type 1,
type 2 and type 3 messages carried in base64 continuations - reusing the HTTP NTLM code, so
`curl --login-options AUTH=NTLM` logs in to the mail servers when `--auth` accepts NTLM.

## Context

- Decisions: BL-185's ADR (whether NTLM is offered, its `--auth` word, failures);
  ADR-0039 (the NTLM challenge and NTLMv2 check, reused as is); ADR-0032 section 3 (NTLM is not
  in the default set).
- Code: `Surl.Authentication.UnitLibrary/NtlmHandshake.cs`, `NtlmChallengeMessage.cs`,
  `NtlmAuthenticateMessage.cs`, `NtlmV2Calculation.cs`, `INtlmServerChallengeSource.cs`. The
  HTTP path wraps these in `WWW-Authenticate`; this path wraps the same messages in the SASL
  exchange's steps. Share, do not copy.
- BL-185's ADR measured what the pinned build sends for SASL NTLM; the tests replay those type 1
  and type 3 messages with a fixed server challenge.

## Acceptance criteria

- [ ] A fast test replays the type 1 and type 3 messages BL-185's ADR recorded, with the recorded
      server challenge, and the login is accepted for the matching account.
- [ ] Fast tests cover a wrong password (refused after the delay), NTLM not in the accepted set
      (the mechanism refused as BL-185's ADR says), and a malformed message.
- [ ] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
