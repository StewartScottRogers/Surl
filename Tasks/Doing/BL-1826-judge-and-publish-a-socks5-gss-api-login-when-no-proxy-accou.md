---
id: BL-1826
title: Judge and publish a SOCKS5 GSS-API login when no proxy account is given
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1828]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-26
completed:
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
  After BL-1828 an `AuthenticationPolicy` over an empty account book refuses it unless `AllowAnonymous`, so the
  policy composed here must not be that.
- No byte Surl sends changes unless a fault rule refuses the login.

## Acceptance criteria

- [ ] A named test in `Surl.Composition.UnitTests` shows that a start with `--keytab` and no proxy account composes a proxy policy whose `JudgeKerberosLoginAsync` publishes one login-judged event for an accepted token.
- [ ] A named test shows the same policy returns `false` for a login a `refuse-login` fault rule matches.
- [ ] A named test shows a start with no proxy account still asks the HTTP proxy for no `Proxy-Authorization` and offers SOCKS5 method `00` as before.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and each changed library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
