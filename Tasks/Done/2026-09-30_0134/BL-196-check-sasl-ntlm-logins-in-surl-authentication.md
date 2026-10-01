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
completed: 2026-09-29
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

- [x] A fast test replays the type 1 and type 3 messages BL-185's ADR recorded, with the recorded
      server challenge, and the login is accepted for the matching account.
- [x] Fast tests cover a wrong password (refused after the delay), NTLM not in the accepted set
      (the mechanism refused as BL-185's ADR says), and a malformed message.
- [x] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- Plan: `NtlmSaslExchange` (a `SaslMechanismExchange`) holds its own `NtlmHandshake`, made by
  `SaslExchangeContext.StartNtlmHandshake` over the policy's `INtlmServerChallengeSource` (a new
  parameter of the internal `AuthenticationPolicy` constructor; the public one passes
  `RandomNtlmServerChallengeSource.Instance`). `NTLM` joins `SaslMechanism.InOfferOrder` after
  `CRAM-MD5` (ADR-0049 section 2), accepted by `--auth ntlm`, offered with or without TLS.
- Choice (sensible default): one `CHALLENGE_MESSAGE` per exchange. A second `NEGOTIATE_MESSAGE`
  is refused as a bad credential after the delay rather than answered again, so a client cannot
  hold an `AUTH` command open with endless challenges; curl never sends one.
- Choice: a malformed message, an empty initial response (`=`) or an `AUTHENTICATE_MESSAGE`
  before any challenge is `RefusedCredentials` after the delay, naming the user when the message
  carried one (ADR-0038 section 6), as CRAM-MD5's malformed response is. Under
  `--allow-anonymous` anything after the type 2 (or a first message that is not a valid type 1)
  ends `AcceptedUnchecked`, per ADR-0049 section 5.
- Curl's recorded type 3 (ADR-0049) is accepted against `user`/`secret` with the fixed challenge
  `0123456789abcdef`, and Surl's type 2 equals the recorded one byte for byte
  (`NtlmSaslMechanismTests`). Quality: 100% line and branch, 0 failing members, worst CRAP 10.
- NTLM not accepted: the `NTLM` row of `MailAuthenticationPolicyTests.Mechanism_UnknownOrNotAccepted_IsRefusedUndelayedWithNoNote`
  (`RefusedMechanism`, undelayed, no note, ADR-0049 section 7).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SASL NTLM logs in to the mail servers: curl's measured type 1/type 3 replay accepted via the shared NtlmHandshake
