---
id: BL-791
title: Publish each judged login and refuse logins by fault in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-05
completed:
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

- [ ] Named tests show a `LoginJudgedEvent` for an accepted and a refused password login, an HTTP Basic login, a
      SASL PLAIN login, an SSH password login and an SMB NTLMv1 login, each with user name, method and verdict; a
      test enumerates every published event's string properties and finds no password.
- [ ] Named tests show a refuse-login rule for `alice` refuses her correct password with the same verdict, note
      and delay as a wrong password, while `bob` logs in; `times=1` fires once.
- [ ] With no publisher or plan given, every existing `Surl.Authentication.UnitTests` test passes unchanged.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean, the whole solution builds,
      the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
