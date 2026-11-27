---
id: BL-1826
title: Judge and publish a SOCKS5 GSS-API login when no proxy account is given
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1838]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-1826 — Judge and publish a SOCKS5 GSS-API login when no proxy account is given

## Goal

Under a start with `--keytab` and neither `-U` nor `--proxy-user-file`, a SOCKS5 GSS-API login publishes its
login-judged event and a `refuse-login` fault rule refuses it.

## Context

- Found while working BL-1817 (ADR-0057 amendment 3). `AuthenticationComposition.ComposeProxyPolicy`
  (`Application/Surl.Composition.UnitLibrary/AuthenticationComposition.cs`) returns `AnonymousAuthenticationPolicy`
  when no proxy account is given. That class does not override `IAuthenticationPolicy.JudgeKerberosLoginAsync`, so
  the default applies, which publishes nothing and refuses nothing.
- ADR-0165 decisions 7 to 9 say the login is published and that `refuse-login` applies; under `surl` that holds only
  with a proxy account. Stand-in rules (root `CLAUDE.md`): observable and fault-injectable.
- `AnonymousAuthenticationPolicy` must keep asking for no login for every other proxy login. If the fix needs another
  project (`Foundation/Surl.Authentication.UnitLibrary` or `Surl.Protocol.Abstractions.UnitLibrary`), add it to
  `touches`. Record a design choice as an ADR-0165 amendment.
- ADR-0165 amendment 1 (BL-1816): with no proxy account, every ticket the keytab's keys accept stays accepted.
  After BL-1838 an `AuthenticationPolicy` over an empty account book refuses it unless `AllowAnonymous`, so the
  policy composed here must not be that.
- No byte Surl sends changes unless a fault rule refuses the login.

## Acceptance criteria

- [x] A named test in `Surl.Composition.UnitTests` shows that a start with `--keytab` and no proxy account composes a proxy policy whose `JudgeKerberosLoginAsync` publishes one login-judged event for an accepted token.
- [x] A named test shows the same policy returns `false` for a login a `refuse-login` fault rule matches.
- [x] A named test shows a start with no proxy account still asks the HTTP proxy for no `Proxy-Authorization` and offers SOCKS5 method `00` as before.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and each changed library keeps 100% line and branch coverage.

## Notes

- Fix, all in `Surl.Composition`: with no proxy account `ComposeProxyPolicy` returns the new
  `NoAccountProxyAuthenticationPolicy`. Password logins and HTTP sessions go to `AnonymousAuthenticationPolicy`;
  `JudgeKerberosLoginAsync` goes to an `AuthenticationPolicy` with no accounts, over the start's events and faults.
- Tests, in `NoAccountProxyAuthenticationPolicyTests`:
  - Box 1: `ComposeProxyPolicy_KeytabAndNoProxyAccount_PublishesOneLoginJudgedEventForAnAcceptedToken`.
  - Box 2: `ComposeProxyPolicy_KeytabNoProxyAccountAndARefuseLoginRule_RefusesTheMatchedLogin`.
  - Box 3: `ComposeProxyPolicy_NoProxyAccount_ProceedsEveryHttpRequestWithNoChallenge` and
    `ComposeAsync_KeytabAndNoProxyAccount_OffersSocks5MethodZeroZeroToAClientOfferingIt`.
  - Wiring through a real start: `ComposeAsync_KeytabAndNoProxyAccount_PublishesARefusedSocks5GssApiToken`.
- That `AuthenticationPolicy` is composed with `AllowAnonymous` true. Why: the code review read
  ADR-0165 amendment 1 (BL-1816) on the shift branch, newer than this lane's base. With no proxy account every
  accepted principal stays accepted, and after BL-1838 an account-less policy refuses it unless `AllowAnonymous`.
  Setting it skips only that account match, so this is correct before and after BL-1838.
- Upstream added `depends-on: [BL-1838]` to this task after the claim. The work does not wait for it, for the
  reason above. BL-1838's context still names `AnonymousAuthenticationPolicy` as the no-account policy; it needs
  no composition change.
- No new ADR amendment: the behaviour is ADR-0165 decisions 7 to 9 and row 1 of amendment 1's table; the wrapper
  is only how the composition builds it, so ADR-0165 was not edited.
- Behaviour change beside publishing: a refused GSS-API token on a start with no proxy account now waits
  `RefusalDelay` (1 s) before `01 FF` (decision 9). No byte Surl sends changes unless a fault rule refuses the login,
  so upstream curl was not measured and no conformance stage ran.
- Verified 2026-11-27: build clean, `dotnet format --verify-no-changes` clean, 30164 fast tests passed (3 skipped),
  `Surl.Composition.UnitLibrary` 100% line and branch.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-27: Doing -> Done. A start with --keytab and no proxy account now publishes each SOCKS5 GSS-API login and applies refuse-login to it, still asking no other proxy login
