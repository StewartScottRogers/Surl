---
id: BL-1277
title: Make TlsSrpAccounts changeable with reset, snapshot and restore
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1244]
touches: [Cryptography/Surl.TlsSrp.UnitLibrary, Cryptography/Surl.TlsSrp.UnitTests]
requirement: FR-061
created: 2026-11-16
completed:
---
# BL-1277 — Make TlsSrpAccounts changeable with reset, snapshot and restore

## Goal

`TlsSrpAccounts` can add and remove an account while handshakes run, reset to its start entries, and take and restore a snapshot.

## Context

ADR-0170 decision 3. No byte on the wire changes (ADR-0122 and ADR-0165 fixtures stay). A snapshot holds user names, salts, group indexes and verifiers, never a password.

## Acceptance criteria

- [ ] `Add(userName, password, ...)` derives the verifier with a salt from a caller-given random source, so a seeded source gives the same verifier each run; `Remove`, `Reset`, `TakeSnapshot` and `Restore` exist and are thread-safe.
- [ ] A handshake after `Add` finds the new account and one after `Remove` finds none (the simulated unknown-user path still applies).
- [ ] `Find` still returns the same answers for an unchanged set (existing tests pass).
- [ ] Surl.TlsSrp keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
