---
id: BL-791
title: Publish each judged login and refuse logins by fault in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-791 — Publish each judged login and refuse logins by fault in Surl.Authentication

## Goal

Every login `Surl.Authentication` judges - password, HTTP, SASL, SSH and SMB - is published on the event stream
with its exchange, scheme, user name, method and verdict (never a secret), and a refuse-login fault rule makes a
matching login be refused exactly as a wrong password is, so a test can assert who logged in and make a login
fail on purpose.

## Context

- Decision: BL-822's ADR decisions 1 (`LoginJudgedEvent`) and 4 (refuse login: scope by account and listener or
  scheme as the login request carries them).
- Contracts: BL-823 (`ISurlEventPublisher`, `IFaultPlan`).
- Where: `Foundation/Surl.Authentication.UnitLibrary` - `AuthenticationPolicy.cs` (the one class behind
  `IAuthenticationPolicy`, `ISaslAuthenticationPolicy`, `IMailAuthenticationPolicy`, `ISshAuthenticationPolicy`,
  `ISmbAuthenticationPolicy`) and `HttpAuthenticationSession.cs`. Optional constructor parameters defaulting to the
  null objects; BL-803 passes the real ones.
- A fault-refused login must be indistinguishable on the wire from a wrong password: same verdict, same fixed
  1-second delay on the injected `TimeProvider`, same note (FR-027, ADR-0032 sections 4, 5 and 8). The fault never
  makes a refused login succeed. Publish a `FaultAppliedEvent` beside the `LoginJudgedEvent`.
- The account the event names is what `Surl.Api`'s queries filter "by account" on (BL-807).
- Stand-in: store = the account set (ADR-0100 decision 4); runtime accounts and reset are BL-802.

## Acceptance criteria

- [x] Named tests show a `LoginJudgedEvent` for an accepted and a refused password login, an HTTP Basic login, a
      SASL PLAIN login, an SSH password login and an SMB NTLMv1 login, each with user name, method and verdict; a
      test enumerates every published event's string properties and finds no password.
- [x] Named tests show a refuse-login rule for `alice` refuses her correct password with the same verdict, note
      and delay as a wrong password, while `bob` logs in; `times=1` fires once.
- [x] With no publisher or plan given, every existing `Surl.Authentication.UnitTests` test passes unchanged.
- [x] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean, the whole solution builds,
      the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Decisions recorded in ADR-0128 (Decided by Claude under Stewart's delegation): optional `events`/`faults`
  constructor parameters on `AuthenticationPolicy` (public and internal constructors); every login carrying a
  `CheckedLogin` is published, plus `CheckPasswordLoginAsync`'s checked verdicts as mechanism `password`; the
  plan is asked only once credentials match (so a wrong password never spends `times`) with the matched account;
  only `RefuseLogin` rules apply; the refusal is each method's own wrong-credential refusal (SCRAM
  `e=invalid-proof`, XOAUTH2/OAUTHBEARER error challenge, SigV4 `BadSignature`, Azure `UnknownAccount` for bearer
  else `BadSignature`, NTLM/Negotiate connection left logged out; DIGEST-MD5 and SCRAM on mail refused before
  `rspauth`/`v=` is sent).
- The login requests carry no exchange ID or listen URL, and HTTP, SSH and SMB carry no scheme, so `ExchangeId`
  is null and those logins are named `http`, `ssh`, `smb`. Filed BL-871 to carry both on the requests (it
  touches Abstractions and the servers, outside this task).
- `touches` gained `Documentation/Planning/Decisions` for ADR-0128 and its README row; no task in Doing names it.
- Tests: `LoginJudgementPolicyTests` (19), plus SCRAM and Azure fault tests in `ScramSaslMechanismTests` and
  `AzureStorageVerifierTests`; `SpendingFaultPlan` spends `times` as BL-792's matcher will. Authentication
  1146 tests pass; every fast test project green; whole solution builds clean.
- Coverage (2026-11-05, `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary`): every changed member
  at 100% line and branch. The members it still lists are not in this diff: `AnswerCheckAsync`'s switch at one
  partial branch (unchanged, noted already by BL-717), the regex generator's output, and pre-existing
  complexity of the Azure members and `GssapiSaslExchange.ServiceOf`.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Authentication publishes a LoginJudgedEvent for every checked login and refuses a login a refuse-login rule matches exactly as a wrong credential
