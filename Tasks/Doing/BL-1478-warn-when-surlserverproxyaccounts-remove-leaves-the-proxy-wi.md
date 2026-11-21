---
id: BL-1478
title: Warn when SurlServerProxyAccounts.Remove leaves the proxy with no account
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1477]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1478 — Warn when SurlServerProxyAccounts.Remove leaves the proxy with no account

## Goal

A test that removes the last proxy account through `Surl.Api` is warned that the proxy now needs no login, as
`Accounts.Remove` warns for server accounts (BL-1391), and can ask whether any proxy account is left.

## Context

- Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1836-Surl-bug-report.md` item 3.
- `Application/Surl.Api/SurlServerProxyAccounts.cs:44-48`: `Remove` returns `stores.RemoveProxyAccount(userName)` and warns
  nothing; it is built at `Application/Surl.Api/SurlServer.cs:52` without the log that `SurlServerAccounts` gets on `:51`
  (`instruments.Log`).
- The pattern to copy: `Application/Surl.Api/SurlServerAccounts.cs:45-76` (the remark, the warning line and `HasAccounts`),
  pinned by `Application/Surl.Api.UnitTests/SurlServerTests.AccountsOpen.cs`.
- BL-1477 adds `ComposedStores.HasProxyAccounts` and fixes the wording
  `surl: warning: no proxy account left; the proxy now needs no login`; use both as they are.
- `Application/Surl.Api/README.md:260-262` documents the server-account case; add the proxy one beside it.
- Stand-in: no event or store change; `Remove` keeps its signature and its `InvalidOperationException` when no proxy store
  is composed.

## Acceptance criteria

- [ ] `SurlServerProxyAccounts` takes the instance log; `Remove` of the last proxy account writes
      `surl: warning: no proxy account left; the proxy now needs no login` once to `SurlServerOptions.Log`, and removing a
      non-last or unknown name writes nothing. Named tests in `Application/Surl.Api.UnitTests` (a new
      `SurlServerTests.ProxyAccountsOpen.cs`) pin both.
- [ ] `SurlServerProxyAccounts.HasAccounts` exists with an XML doc, over `ComposedStores.HasProxyAccounts`, and a named test pins
      it true, then false after the last removal.
- [ ] `Remove`'s XML remarks and `Application/Surl.Api/README.md` (beside the `Accounts.Remove` paragraph) say that removing the
      last proxy account leaves the proxy needing no login, warned.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green and `Surl.Api` keeps 100% line and
      branch coverage.

## Notes

- `surl-mcp`'s `remove_proxy_account` follows in BL-1479.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
