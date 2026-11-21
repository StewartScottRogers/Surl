---
id: BL-1491
title: Add SetPassword to Surl.Api's three account views per ADR-0132 amendment 1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1477, BL-1482, BL-1493, BL-1494]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1491 — Add SetPassword to Surl.Api's three account views per ADR-0132 amendment 1

## Goal

`Surl.Api`'s three account views gain `SetPassword(user, password)`.

## Context

- ADR-0132 amendment 1 (`Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md`) states the rule, the answers and the events; follow it exactly.

## Acceptance criteria

- [ ] `Accounts.SetPassword`, `ProxyAccounts.SetPassword` and `TlsSrpAccounts.SetPassword` change the password and throw `ArgumentException` for an unknown user, as amendment 1 says; tests prove each.
- [ ] `Add` of an existing name still throws `ArgumentException` and its document comment says to use `SetPassword`.

## Notes

- 2026-11-20: Surl.Api reaches the three stores only through `ComposedStores` (Surl.Composition), which has no `SetPassword` wrapper: the proxy book and TLS-SRP accounts are private/internal there, and `AddAccount` also handles the mail owner. The Api views need `ComposedStores.SetAccountPassword`, `SetProxyAccountPassword` and `SetTlsSrpAccountPassword` (publish `AccountRemoved` then `AccountAdded`; mail owner retire and reach again for the account book per ADR-0185), so `touches` gains the Composition projects. BL-1477 (in Doing) touches Composition, so this goes back to Backlog until it is Done. BL-1505 (proxy view only) overlaps this task's Api files and should be merged into it or closed when this lands.

## Log
- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Backlog. Needs ComposedStores SetPassword wrappers in Application/Surl.Composition.UnitLibrary, which BL-1477 (Doing) touches; depends-on BL-1477
- 2026-11-21: Backlog -> Doing.
