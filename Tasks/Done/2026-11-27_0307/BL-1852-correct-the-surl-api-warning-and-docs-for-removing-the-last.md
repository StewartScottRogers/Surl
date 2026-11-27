---
id: BL-1852
title: Correct the Surl.Api warning and docs for removing the last proxy account
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1851]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1852 — Correct the Surl.Api warning and docs for removing the last proxy account

## Goal

`Surl.Api` writes and documents what ADR-0132 amendment 4 says removing the last proxy account leaves.

## Context

- ADR-0132 amendment 4, decisions 2 to 6.
- `SurlServerProxyAccounts.Remove` writes the old line; its remarks and `HasAccounts` say the proxy needs no login;
  `Application/Surl.Api/README.md` quotes both; the line is pinned by `SurlServerTests.ProxyAccountsOpen`.
- The server accounts' warning (`SurlServerAccounts`) is BL-1854's question, not this task's.

## Acceptance criteria

- [x] `Remove` of the last proxy account writes
      `surl: warning: no proxy account left; without --allow-anonymous the proxy now lets no new client through`,
      pinned by `SurlServerTests.ProxyAccountsOpen`.
- [x] A named test starts a server with one proxy account, removes it, and gets the `407` for a `GET` through the
      proxy without credentials.
- [x] The XML documents of `Remove` and `HasAccounts`, the README and the project's `CLAUDE.md` say the proxy then
      lets no new client through without `--allow-anonymous`; none says it needs no login.

## Notes

- 2026-11-27: Changed the line in `SurlServerProxyAccounts.Remove`, its XML documents and `HasAccounts`', the README
  and `CLAUDE.md` (which said nothing about removing the last proxy account before).
- The 407 test is `SurlServerTests.ProxyAccountsRemove_TheLastAccount_AnswersAGetWithoutCredentialsWith407`. It passed
  before this change, because BL-1851 already composes the proxy to keep asking for a login; the warning test failed
  against the old line first. No new wire byte, so nothing to measure (ADR-0132 amendment 4, decision 8).
- Verify: build 0 warnings, format clean, fast tests 30219 passed, 0 failed, 3 skipped (`Surl.Api.UnitTests` 415).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Surl.Api warns and documents that removing the last proxy account lets no new client through without --allow-anonymous; a GET without credentials gets the 407
