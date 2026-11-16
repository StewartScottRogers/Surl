---
id: BL-1280
title: Surl.Api: add and remove proxy and TLS-SRP accounts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1244, BL-1279]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: FR-061
created: 2026-11-16
completed:
---
# BL-1280 — Surl.Api: add and remove proxy and TLS-SRP accounts

## Goal

A test using `Surl.Api` adds and removes proxy and TLS-SRP accounts on a running instance.

## Context

ADR-0170 decision 4, after BL-1279. Shaped like the existing `Accounts` member.

## Acceptance criteria

- [ ] `ProxyAccounts` and `TlsSrpAccounts` members each have `Add` and `Remove`, delegating to `ComposedStores`.
- [ ] A test adds a proxy account and logs in through the HTTP proxy, removes it and is refused.
- [ ] A test adds a TLS-SRP account and its handshake verifies.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
