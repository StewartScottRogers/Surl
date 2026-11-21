---
id: BL-1477
title: Warn when the control endpoint removes a server's last account or last proxy account
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1477 — Warn when the control endpoint removes a server's last account or last proxy account

## Goal

A test suite driving `surl --control` is warned, as `Surl.Api` and `remove_account` already warn (BL-1391), when
`DELETE /v1/accounts/{user}` or `DELETE /v1/proxy-accounts/{user}` leaves the server or its proxy with no account
and so needing no login.

## Context

- Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1836-Surl-bug-report.md` item 3:
  removing the last account leaves the server open (an unauthenticated `GET` answers `200`); `Surl.Api` and
  `remove_account` warn, the control endpoint and `remove_proxy_account` do not.
- BL-1391's rule (secure by default: removal stays allowed, and is warned): `Application/Surl.Api/SurlServerAccounts.cs:60-70`
  writes `surl: warning: no account left; the server now needs no login` to the instance log;
  `Application/Surl.Mcp.UnitLibrary/RemoveAccountTool.cs:57` adds a `warning` member.
- Control path: `Application/Surl.Control.UnitLibrary/ControlStoreRequests.cs:152-164` calls
  `ComposedControlTarget.RemoveAccountAsync` / `RemoveProxyAccountAsync`
  (`Application/Surl.Composition.UnitLibrary/ComposedControlTarget.cs:184-198`), which return only whether a name was
  removed. The target is built at `Application/Surl.Composition.UnitLibrary/ComposedServing.cs:202` with no log;
  `SurlServingOutputs.Log` (`SurlServingOutputs.cs:17`) is the instance log to hand it.
- Proxy accounts: `ComposedStores.RemoveProxyAccount` (`Application/Surl.Composition.UnitLibrary/ComposedStores.AccountStores.cs:35`)
  has no "any left" query; `ComposedStores.HasAccounts` (`ComposedStores.cs:656`) is the server accounts' one. Add
  `ComposedStores.HasProxyAccounts` beside it (false when no proxy account store is composed), which BL-1478 then uses.
- The answer stays `204` as ADR-0124 decision 7's table says; the warning is a log line, and the event stream
  already carries the `AccountRemoved` store change. Proxy wording, following BL-1391's:
  `surl: warning: no proxy account left; the proxy now needs no login`.
- Stand-in: account store only; no new event kind, no fault, no seed or reset change.

## Acceptance criteria

- [ ] `ComposedControlTarget` takes the instance log and, after a removal that leaves no account, writes
      `surl: warning: no account left; the server now needs no login` once; removing a non-last account or an unknown
      name writes nothing. Named tests in `Application/Surl.Composition.UnitTests` (`ComposedControlTargetTests` or a new
      `ComposedControlTargetAccountRemovalTests`) pin both.
- [ ] The same for the last proxy account with `surl: warning: no proxy account left; the proxy now needs no login`,
      named tests included.
- [ ] `ComposedStores.HasProxyAccounts` exists with an XML doc and a named test (true, false after the last removal, false
      with no proxy store).
- [ ] A named test in `SurlServerCompositionTests.Control.cs` drives `DELETE /v1/accounts/<last>` through the served
      control endpoint and finds the warning line in the log and `204` in the answer.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, the fast tests are green and
      `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- `Surl.Api`'s `SurlServerProxyAccounts.Remove` (BL-1478) and `surl-mcp`'s `remove_proxy_account` (BL-1479) follow.

## Log

- 2026-11-20: Created.
