---
id: BL-1490
title: Make the control target refuse a duplicate account and add the set-password routes per ADR-0132 amendment 1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1482, BL-1493, BL-1494]
touches: [Documentation/Planning/Decisions, Documentation/Planning/Decisions, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1490 — Make the control target refuse a duplicate account and add the set-password routes per ADR-0132 amendment 1

## Goal

`ComposedControlTarget` and the control endpoint answer `POST` on an existing account with `409 conflict` and gain `PUT` set-password routes for accounts, proxy accounts and TLS-SRP accounts.

## Context

- ADR-0132 amendment 1 (`Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md`) states the rule, the answers and the events; follow it exactly.

## Acceptance criteria

- [x] `POST /v1/accounts` for a name that exists answers `409 conflict` and keeps the account; `PUT /v1/accounts/{user}` answers `204`, `404 not_found` or `400 bad_request` per the amendment; the same for `/v1/proxy-accounts` and `/v1/tls-srp-accounts`; tests prove each.
- [x] `IControlTarget`'s member documents no longer say replace; ADR-0124 decision 7's table and ADR-0185 decision 2's sentence are updated as amendment 1 lists (add them to touches first).

## Notes

## Log
- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. Control target refuses a duplicate account (409) and PUT set-password routes exist for accounts, proxy and TLS-SRP accounts
