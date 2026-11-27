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
completed:
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

- [ ] `Remove` of the last account writes
      `surl: warning: no local account left; the server still asks for a login wherever it did`, with and without
      `--user-ldap`, pinned by `SurlServerTests.AccountsOpen`.
- [ ] A named test starts an `http` server with one account, removes it, and gets `401` for a `GET` without
      credentials.
- [ ] A named test starts an `http` server with no account, adds and removes one, gets `401`, calls `ResetAsync`, and
      gets the file.
- [ ] The XML documents of `Remove` and `HasAccounts`, the README and the project's `CLAUDE.md` say removal does not
      open the server and that `HasAccounts` `false` means no account is held; none says it needs no login.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
