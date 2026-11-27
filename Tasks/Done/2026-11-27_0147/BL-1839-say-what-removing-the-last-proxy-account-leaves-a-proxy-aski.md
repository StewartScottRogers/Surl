---
id: BL-1839
title: Decide what removing the last proxy account leaves a proxy asking for, and what the warning says
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-1839 — Decide what removing the last proxy account leaves a proxy asking for, and what the warning says

## Goal

An ADR states what a proxy asks for after its last proxy account is removed while the server runs, and what the warning then says.

## Context

- Found by BL-1816 by reading the source; no test was run. Confirm each statement below in the source before deciding.
- `ComposedControlTarget.RemoveProxyAccountAsync` (`Application/Surl.Composition.UnitLibrary`), `SurlServerProxyAccounts.Remove` (`Application/Surl.Api`) and `RemoveProxyAccountTool` (`Application/Surl.Mcp.UnitLibrary`) say the proxy "now needs no login".
- The proxy policy composed at start is still an `AuthenticationPolicy` over the now empty book. `AuthenticationPolicy.JudgePasswordLogin` answers a login with no user name `RefusedAnonymous` unless `--allow-anonymous`, so SOCKS5 should refuse method `00` and every `02` login, and SOCKS4 answer `5B`. `HttpAuthenticationSession.JudgeWithoutCredentials` still challenges a request with `IsWrite`. ADR-0165 amendment 1 decision 2 refuses every GSS-API login too.
- Weigh secure by default (root `CLAUDE.md`): removing accounts should not open a proxy unasked. See ADR-0088 decision 5 and ADR-0132.
- The text is pinned by `ComposedControlTargetAccountRemovalTests`, `SurlServerTests.ProxyAccountsOpen` and `ProxyAndTlsSrpAccountToolsTests`; `Application/Surl.Api/README.md` quotes it.
- Stand-in: a test that removes accounts between exchanges relies on this line to know what the next exchange will get.

## Acceptance criteria

- [x] An amendment to ADR-0132, marked "Decided by Claude under Stewart's delegation", states what an HTTP proxy `GET`, an HTTP proxy `CONNECT`, a SOCKS4 request and each SOCKS5 method get after the last proxy account is removed, and the warning's text on the control endpoint, `Surl.Api` and `surl-mcp`.
- [x] The code and text tasks the decision needs are filed in `Tasks/Backlog`, or the amendment says none is needed.

## Notes

- 2026-11-27: Confirmed in the source (no test run). One statement in Context is incomplete: after the removal the
  HTTP proxy challenges only writes, so a `GET` or `HEAD` without `Proxy-Authorization` is forwarded. That is the
  proxy opening unasked.
- Decided (ADR-0132 amendment 4): the proxy stays closed, `GET` and `HEAD` get the `407` too, through a new
  `AuthenticationSettings.RequiresLoginWithoutAccounts`; `--allow-anonymous` unchanged; one warning text true with
  and without it. Rejected: keeping the opening and only rewording, refusing the removal, calling every proxy method
  a write.
- Nothing measured: no byte changes; ADR-0088 amendment 3 already measured curl against the proxy's `407`.
- Filed with the board script: BL-1850, BL-1851, BL-1852, BL-1853 (code and text), BL-1854 (the same question for
  the server accounts, out of this task's scope).
- Edited in the session rather than through `align-and-document`: two files, and the decision was already read out of
  the source here. No `.cs` or project file changed.

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ADR-0132 amendment 4: a proxy composed with proxy accounts stays closed when the last is removed, GET and HEAD included; warning text decided; BL-1850 to BL-1854 filed
