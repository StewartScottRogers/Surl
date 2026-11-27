---
id: BL-1838
title: Require a proxy account named for the principal for a SOCKS5 GSS-API login
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1838 — Require a proxy account named for the principal for a SOCKS5 GSS-API login

## Goal

A SOCKS5 GSS-API login on a proxy started with proxy accounts is accepted only when a proxy account has the client principal's name, unless `--allow-anonymous`.

## Context

- Decided by BL-1816: ADR-0165 amendment 1 (`Documentation/Planning/Decisions/`). Follow its decision table and decisions 2 to 4.
- Today `AuthenticationPolicy.JudgeKerberosLoginAsync` (`Foundation/Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs`) accepts any principal the keytab's keys accept, so GSS-API is a way past the `-U`/`--proxy-user-file` accounts.
- The other Kerberos logins already do this with `settings.Accounts.HasNamedAccount(name)` (`NegotiateKerberosLogin.Decide`, `GssapiSaslExchange`). The policy SOCKS asks is the proxy policy `AuthenticationComposition.ComposeProxyPolicy` builds, so its account book is the proxy accounts; no composition change is expected.
- `Socks5GssApiNegotiation` already answers a `false` verdict with `01 FF` and no AP-REP, so no byte and no SOCKS code changes.
- `KerberosLoginJudgementPolicyTests` (`Foundation/Surl.Authentication.UnitTests`) builds its book with the account `alice` and the principal `alice@EXAMPLE.TEST`; its accepted cases need an account of the principal's name.
- A policy composed without proxy accounts is `AnonymousAuthenticationPolicy`, which keeps accepting any ticket (BL-1826 is about publishing there).
- Stand-in: a test seeds, removes, resets and restores the proxy account named for the principal; each change counts at the next login. Refusals show as `LoginJudgedEvent`.
- Help text is BL-1819's.

## Acceptance criteria

- [ ] Named tests in `Surl.Authentication.UnitTests` show an accepted token is accepted when an account has exactly the principal's name, and refused (names differing in case or realm included) after `RefusalDelay` when none has, publishing one `LoginJudgedEvent` with `Mechanism` `gssapi`, `Account` the principal, `IsAccepted` false and `IsRefusedByFault` false.
- [ ] A named test shows a `refuse-login` rule with `times` is not spent by a principal that has no account.
- [ ] A named test shows that with `--allow-anonymous` (`AllowAnonymous`) an accepted token with no account is accepted and published, and a matching `refuse-login` rule still refuses it.
- [ ] A named test shows an empty account book refuses an accepted token, and an account added while the policy lives makes the next login stand.
- [ ] The XML comment on `JudgeKerberosLoginAsync` and the **GSS-API** paragraph of `Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/CLAUDE.md` state the account rule of ADR-0165 amendment 1.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Authentication.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-26: Created.
