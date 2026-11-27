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
completed:
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

- [ ] ADR-0165 (an amendment) states whether a SOCKS5 GSS-API login needs an account of the principal's name, which options loosen that, and why.
- [ ] If the decision changes behaviour, the code and help text tasks it needs are filed in `Tasks/Backlog`, and BL-1819's context names the decision.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
