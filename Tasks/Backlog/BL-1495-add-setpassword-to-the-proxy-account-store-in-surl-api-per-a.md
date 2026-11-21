---
id: BL-1495
title: Add SetPassword to the proxy account store in Surl.Api per ADR-0132 amendment 1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1482]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1495 — Add SetPassword to the proxy account store in Surl.Api per ADR-0132 amendment 1

## Goal

`SurlServerProxyAccounts.SetPassword(user, password)` exists on `Surl.Api` as ADR-0132 amendment 1 says (the proxy store sits in `Application/Surl.Api/SurlServerProxyAccounts.cs`, split from BL-1494).

## Context

- ADR-0132 amendment 1 (rule 2 and 3, the answers table). BL-1494 added `TlsSrpAccounts.SetPassword(user, password, random)` as the model: unknown user and empty password refused with `ArgumentException`; publishes `AccountRemoved` then `AccountAdded`.

## Acceptance criteria

- [ ] `SetPassword` on the proxy accounts re-derives what Add derives, keeps other members, publishes `AccountRemoved` then `AccountAdded`, refuses an unknown user and an empty password; unit tests prove each.

## Notes

## Log

- 2026-11-20: Created.
