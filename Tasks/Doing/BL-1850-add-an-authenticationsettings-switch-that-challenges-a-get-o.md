---
id: BL-1850
title: Add an AuthenticationSettings switch that challenges a GET or HEAD without credentials when no account is left
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-1839]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1850 — Add an AuthenticationSettings switch that challenges a GET or HEAD without credentials when no account is left

## Goal

`AuthenticationSettings.RequiresLoginWithoutAccounts`, `false` by default, makes an HTTP session challenge a
request with no credentials even when there is no account to log in to and the request is not a write.

## Context

- ADR-0132 amendment 4, decision 7.
- `HttpAuthenticationSession.JudgeWithoutCredentials` challenges only when
  `Settings.HasAccountToLogInTo(scheme) || request.IsWrite`.
- The proxy needs it so that removing its last account does not let `GET` and `HEAD` through (BL-1851 sets it).

## Acceptance criteria

- [ ] With the switch `true`, an empty account book and no `--allow-anonymous`, a `GET` and a `HEAD` without
      credentials are answered `Challenge` with the offered challenges, pinned by a named test.
- [ ] With the switch `true` and no accepted method that can be offered (Basic alone, unencrypted), the same request
      is `Forbidden`, pinned by a named test.
- [ ] With the switch `true`, `AllowAnonymous` still proceeds, and a connection NTLM logged in still proceeds as that
      account, each pinned by a named test.
- [ ] With the switch at its default, a `GET` without credentials and an empty book still proceeds (existing or new
      test), and a `ForScheme` view keeps the switch's value.
- [ ] The XML documents of the switch and the library's `CLAUDE.md` say what it does.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
