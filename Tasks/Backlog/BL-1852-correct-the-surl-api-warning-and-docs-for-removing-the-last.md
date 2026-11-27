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
completed:
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

- [ ] `Remove` of the last proxy account writes
      `surl: warning: no proxy account left; without --allow-anonymous the proxy now lets no new client through`,
      pinned by `SurlServerTests.ProxyAccountsOpen`.
- [ ] A named test starts a server with one proxy account, removes it, and gets the `407` for a `GET` through the
      proxy without credentials.
- [ ] The XML documents of `Remove` and `HasAccounts`, the README and the project's `CLAUDE.md` say the proxy then
      lets no new client through without `--allow-anonymous`; none says it needs no login.

## Notes

## Log

- 2026-11-27: Created.
