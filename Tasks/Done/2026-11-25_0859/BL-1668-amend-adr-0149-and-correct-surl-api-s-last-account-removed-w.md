---
id: BL-1668
title: Amend ADR-0149 and correct Surl.Api's last-account-removed warning for a configured directory
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation, Application/Surl.Api]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1668 — Amend ADR-0149 and correct Surl.Api's last-account-removed warning for a configured directory

## Goal

A configured `--user-ldap` directory counts as an account in ADR-0149, and `Accounts.Remove` of the last local account no longer says the server needs no login when a directory is configured.

## Context

Follows BL-1663 (a directory-only HTTP server now challenges reads). ADR-0032 section 4, ADR-0149 decision 6.

## Acceptance criteria

- [x] ADR-0149 has an amendment recording that a configured directory is an account for the login requirement.
- [x] `SurlServerAccounts.Remove` writes a true warning when a directory is configured, pinned by a test in `Surl.Api.UnitTests`.
- [x] `Surl.Api`'s README and XML docs say so.

## Notes

- Decided by Claude under Stewart's delegation: with `--user-ldap` the warning reads `surl: warning: no local account left; logins are now checked against the directory only`; without it, unchanged. `HasAccounts` still reports local accounts only. The directory's `protocols=` is not consulted by the warning (it is wording about the account source, not a per-scheme verdict).
- Fix: `SurlServer` passes `commandLine.UserLdap is not null` to `SurlServerAccounts` (no `touches` change; the control endpoint's `ComposedControlTarget` has the same stale warning, in `Surl.Composition`, outside this task's `touches`; filed as BL-1671).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. ADR-0149 amended; Surl.Api warning corrected for a configured directory
