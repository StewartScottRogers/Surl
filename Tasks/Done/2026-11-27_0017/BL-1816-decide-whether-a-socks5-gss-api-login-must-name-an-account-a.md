---
id: BL-1816
title: Decide whether a SOCKS5 GSS-API login must name an account, as other Kerberos logins do
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0165-how-tls-srp-and-socks5-gss-api-logins-publish-login-judged-events-and-honour-refuse-login.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1816 — Decide whether a SOCKS5 GSS-API login must name an account, as other Kerberos logins do

## Goal

An ADR states whether a SOCKS5 GSS-API login whose ticket the keytab accepts is also required to name an account.

## Context

Found by BL-1809. `AuthenticationPolicy.JudgeKerberosLoginAsync`
(`Foundation/Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs`) takes the accepted token's client
principal as the account name and looks up no account, so any principal the keytab's keys accept may use the
SOCKS proxy, unless a `refuse-login` fault rule refuses it. `--keytab`'s manual text
(`Application/Surl.Cli.UnitLibrary/ManualText.cs`) says a Kerberos login is accepted only when an account of
exactly that name exists. Weigh secure by default (root `CLAUDE.md`) and what ADR-0057, ADR-0121 amendment 2
and ADR-0165 already decide. BL-1819 changes the same help text.

## Acceptance criteria

- [x] ADR-0165 (an amendment) states whether a SOCKS5 GSS-API login needs an account of the principal's name, which options loosen that, and why.
- [x] If the decision changes behaviour, the code and help text tasks it needs are filed in `Tasks/Backlog`, and BL-1819's context names the decision.

## Notes

- Decision (ADR-0165 amendment 1): yes when the proxy has proxy accounts. A SOCKS5 GSS-API login then needs a
  proxy account of exactly the principal's name; `--allow-anonymous` skips the match. A proxy started with no
  proxy account asks no login (method `00` is open), so any ticket the keys accept stays accepted. Why: a keytab
  must not widen a proxy account list to a whole realm (ADR-0057), and refusing where `00` is open protects nothing.
- The behaviour changes: BL-1838 (code). BL-1819 names the decision and now depends on BL-1838 too. BL-1826 also
  depends on BL-1838, so the policy it composes keeps accepting any ticket with no proxy account.
- Found by reading, not tested: the "proxy now needs no login" warning looks false once the last proxy account is
  removed. Filed BL-1839 to confirm and decide.
- Written in the lane session rather than by `align-and-document`: the decision needed the same reading, and a
  second agent would have repeated it.
- Nothing measured: no byte Surl sends is new (a refusal is the existing `01 FF`).

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. ADR-0165 amendment 1 decides a SOCKS5 GSS-API login needs a proxy account of the principal's name when the proxy has proxy accounts; BL-1838 and BL-1839 filed
