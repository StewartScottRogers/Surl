---
id: BL-1866
title: Correct the Surl.Api warning and docs for removing the last server account
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1863, BL-1852]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1866 — Correct the Surl.Api warning and docs for removing the last server account

## Goal

`Surl.Api` writes and documents what ADR-0132 amendment 5 says removing the last server account leaves.

## Context

- ADR-0132 amendment 5, decisions 1 to 3 and 8.
- `SurlServerAccounts.Remove` writes one of two old lines, chosen by `hasDirectory`; its remarks and `HasAccounts`
  say the server needs no login; `Application/Surl.Api/README.md` quotes the line. The lines are pinned by
  `SurlServerTests.AccountsOpen`.
- The proxy accounts' warning is BL-1852's.

## Acceptance criteria

- [x] `Remove` of the last account writes
      `surl: warning: no local account left; the server still asks for a login wherever it did`, with and without
      `--user-ldap`, pinned by `SurlServerTests.AccountsOpen`.
- [x] A named test starts an `http` server with one account, removes it, and gets `401` for a `GET` without
      credentials.
- [x] A named test starts an `http` server with no account, adds and removes one, gets `401`, calls `ResetAsync`, and
      gets the file.
- [x] The XML documents of `Remove` and `HasAccounts`, the README and the project's `CLAUDE.md` say removal does not
      open the server and that `HasAccounts` `false` means no account is held; none says it needs no login.

## Notes

- Tests, in `SurlServerTests.AccountsOpen.cs`:
  `AccountsRemove_TheLastAccount_WarnsInTheLogAndHasAccountsTurnsFalse` and
  `AccountsRemove_TheLastLocalAccountWithADirectory_WritesTheSameWarning` pin the line;
  `AccountsRemove_TheLastAccount_AnswersAGetWithoutCredentialsWith401` and
  `AccountsRemove_TheOnlyAccountAddedWhileRunning_AnswersAGetWith401UntilResetAsync` cover the other two.
- One line is true with and without `--user-ldap`, so `SurlServerAccounts`' `hasDirectory` parameter and
  `ServerInstruments.HasDirectory` had no use left and are removed.
- Verified: build clean (0 warnings), `dotnet format --verify-no-changes` clean, fast tests all pass
  (`Surl.Api.UnitTests` 417 of 417). No integration-level code changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Surl.Api warns and documents that removing the last server account leaves the server still asking for a login wherever it did
