---
id: BL-295
title: Check SMB NTLMv1 session-setup logins in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-294, BL-291]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-052
created: 2026-09-30
completed:
---
# BL-295 — Check SMB NTLMv1 session-setup logins in Surl.Authentication

## Goal

`Surl.Authentication`'s `AuthenticationPolicy` implements BL-294's SMB contract: it checks an SMB
session setup's NT (and, as BL-283's ADR decides, LM) response against each account's NTLMv1
calculation (BL-291), refuses every login when no account is configured, and applies the ADR's
rule for NTLMv1 over a connection without TLS.

## Context

- Decisions: BL-283's ADR (what is accepted, which loosening option or `--auth` word NTLMv1 over
  plain `smb://` needs, how the domain is matched, anonymous and guest sessions under
  `--allow-anonymous`, the login note); ADR-0032 (secure by default, section 6's contract, the
  warnings); ADR-0038 (the login note travels in the verdict).
- Code: `Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs` (already implements
  `IAuthenticationPolicy`, `IMailAuthenticationPolicy` and `ISshAuthenticationPolicy`),
  `NtlmAccount.cs`, `ChallengeResponseAccount.cs`, `CryptographicSecretComparer.cs` (compare in
  constant time), `AuthenticationSettings.cs` (which methods `--auth` enables), and BL-291's
  NTLMv1 calculation.
- A wrong user, a wrong password and an unknown account are indistinguishable to the caller
  (ADR-0006 section 3); the login note says which, for the server's log.

## Acceptance criteria

- [ ] Tests in `Surl.Authentication.UnitTests` show: the [MS-NLMP] 4.2.2 responses accepted for user
      `User`, password `Password`; a wrong response refused; no accounts refused; the ADR's rule for
      a connection without TLS applied both ways; each refusal carrying the ADR's login note.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Authentication.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
