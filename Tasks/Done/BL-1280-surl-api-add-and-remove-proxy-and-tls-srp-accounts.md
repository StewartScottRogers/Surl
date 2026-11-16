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
completed: 2026-11-16
---
# BL-1280 — Surl.Api: add and remove proxy and TLS-SRP accounts

## Goal

A test using `Surl.Api` adds and removes proxy and TLS-SRP accounts on a running instance.

## Context

ADR-0170 decision 4, after BL-1279. Shaped like the existing `Accounts` member.

## Acceptance criteria

- [x] `ProxyAccounts` and `TlsSrpAccounts` members each have `Add` and `Remove`, delegating to `ComposedStores`.
- [x] A test adds a proxy account and logs in through the HTTP proxy, removes it and is refused.
- [x] A test adds a TLS-SRP account and its handshake verifies.

## Notes

- The TLS-SRP test reads the accounts through the `ServerTlsSettings` the host seam is given (the fake listener does no handshake): it checks the added account is not simulated and its verifier is the SRP verifier of salt, user and password; removal returns it to simulated.
- Fixed on the way: `CustomProtocolServers.InOrderGiven` threw when the command line added a listen URL (a proxy from `AdditionalArguments`); those URLs now follow the given ones.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. ProxyAccounts and TlsSrpAccounts Add/Remove on Surl.Api, tested
