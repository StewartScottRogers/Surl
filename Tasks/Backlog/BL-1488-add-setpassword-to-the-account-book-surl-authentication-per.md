---
id: BL-1488
title: Add SetPassword to the account book (Surl.Authentication) per ADR-0132 amendment 1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1482]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1488 — Add SetPassword to the account book (Surl.Authentication) per ADR-0132 amendment 1

## Goal

`AccountBook` (and its scheme views) gain `SetPassword(user, password)` as ADR-0132 amendment 1 decision 3 says.

## Context

- ADR-0132 amendment 1 (`Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md`) states the rule, the answers and the events; follow it exactly.

## Acceptance criteria

- [ ] `SetPassword` re-derives every credential, keeps scope, mailbox and groups, publishes `AccountRemoved` then `AccountAdded`, and throws `ArgumentException` for an unknown user or an empty password; unit tests prove each, with the 100% coverage gates held.
- [ ] Reset returns to the configured password; a snapshot restores a changed password.

## Notes

## Log
- 2026-11-20: Created.
