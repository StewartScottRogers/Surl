---
id: BL-1671
title: Correct ComposedControlTarget's last-account-removed warning for a configured directory
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-1671 — Correct ComposedControlTarget's last-account-removed warning for a configured directory

## Goal

The control endpoint (`ComposedControlTarget.RemoveAccountAsync`) warns truthfully when the last local account is removed on a server with `--user-ldap`, as `SurlServerAccounts.Remove` does (ADR-0149 amendment 1, BL-1668).

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] A test pins the directory wording `surl: warning: no local account left; logins are now checked against the directory only`.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
