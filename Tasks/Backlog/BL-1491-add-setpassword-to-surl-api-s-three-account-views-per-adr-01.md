---
id: BL-1491
title: Add SetPassword to Surl.Api's three account views per ADR-0132 amendment 1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1482, BL-1493, BL-1494]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
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

## Log
- 2026-11-20: Created.
