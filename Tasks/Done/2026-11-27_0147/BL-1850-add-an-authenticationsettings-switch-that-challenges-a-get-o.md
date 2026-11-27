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
completed: 2026-11-27
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

- [x] With the switch `true`, an empty account book and no `--allow-anonymous`, a `GET` and a `HEAD` without
      credentials are answered `Challenge` with the offered challenges, pinned by a named test.
- [x] With the switch `true` and no accepted method that can be offered (Basic alone, unencrypted), the same request
      is `Forbidden`, pinned by a named test.
- [x] With the switch `true`, `AllowAnonymous` still proceeds, and a connection NTLM logged in still proceeds as that
      account, each pinned by a named test.
- [x] With the switch at its default, a `GET` without credentials and an empty book still proceeds (existing or new
      test), and a `ForScheme` view keeps the switch's value.
- [x] The XML documents of the switch and the library's `CLAUDE.md` say what it does.

## Notes

- `JudgeWithoutCredentials` now challenges on `RequiresLoginWithoutAccounts || HasAccountToLogInTo(scheme) || IsWrite`; `--allow-anonymous` and the connection login are judged first, as before. No new ADR: ADR-0132 amendment 4, decision 7, already decides it.
- Tests: `HttpAuthenticationSessionTests.JudgeAsync_RequiringLogin*` and `JudgeAsync_NotRequiringLoginWithNoAccount_ProceedsOnAReadWithoutCredentials`; `AuthenticationSettingsTests.RequiresLoginWithoutAccounts_ByDefault_IsFalse` and `ForScheme_KeepsRequiresLoginWithoutAccounts`.
- Verify: build clean, format clean, fast tests 30208 passed, 0 failed, 3 skipped.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. AuthenticationSettings.RequiresLoginWithoutAccounts challenges a GET or HEAD without credentials when no account is left; off by default
