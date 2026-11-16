---
id: BL-1276
title: Add StoreKind.ProxyAccounts and StoreKind.TlsSrpAccounts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1244]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-061
created: 2026-11-16
completed:
---
# BL-1276 — Add StoreKind.ProxyAccounts and StoreKind.TlsSrpAccounts

## Goal

`StoreKind` names the proxy account store and the TLS-SRP account store so events, snapshots and filters can address them.

## Context

ADR-0170 decisions 2 and 3. Members are appended; existing members keep their values and names.

## Acceptance criteria

- [ ] `StoreKind` has `ProxyAccounts` and `TlsSrpAccounts` with XML docs, after the existing members.
- [ ] Existing tests pinning the member list or names are updated and pass.
- [ ] The Abstractions project keeps its quality gates.

## Notes

## Log

- 2026-11-16: Created.
