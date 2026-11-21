---
id: BL-1505
title: Add SetPassword to the proxy account store in Surl.Api per ADR-0132 amendment 1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1482]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1505 — Add SetPassword to the proxy account store in Surl.Api per ADR-0132 amendment 1

## Goal

`SurlServerProxyAccounts.SetPassword(user, password)` exists on `Surl.Api` as ADR-0132 amendment 1 says (the proxy store sits in `Application/Surl.Api/SurlServerProxyAccounts.cs`, split from BL-1494).

## Context

- ADR-0132 amendment 1 (rule 2 and 3, the answers table). BL-1494 added `TlsSrpAccounts.SetPassword(user, password, random)` as the model: unknown user and empty password refused with `ArgumentException`; publishes `AccountRemoved` then `AccountAdded`.

## Acceptance criteria

- [x] `SetPassword` on the proxy accounts re-derives what Add derives, keeps other members, publishes `AccountRemoved` then `AccountAdded`, refuses an unknown user and an empty password; unit tests prove each.

## Notes

- SetPassword is built in Surl.Api only: validates, then RemoveProxyAccount (false = unknown user, ArgumentException) and AddProxyAccount, so the store publishes AccountRemoved then AccountAdded and the last-account warning (Api.Remove only) never fires. TlsSrpAccounts.SetPassword (BL-1494) is not in this checkout, so no copy of its code was used.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. SurlServerProxyAccounts.SetPassword exists and is tested
