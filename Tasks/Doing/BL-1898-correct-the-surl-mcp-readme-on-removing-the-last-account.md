---
id: BL-1898
title: Correct the Surl.Mcp README on removing the last account
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1859]
touches: [Application/Surl.Mcp/README.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1898 — Correct the Surl.Mcp README on removing the last account

## Goal

The `surl-mcp` README states what removing a server's last account does once BL-1859 has changed the warning.

## Context

- ADR-0132 amendment 5, decisions 1 and 8.
- `Application/Surl.Mcp/README.md` quotes `server <id> has no account left and now needs no login` for
  `remove_account`. BL-1859 touches only `Surl.Mcp.UnitLibrary` and its tests. Found by BL-1862.

## Acceptance criteria

- [ ] The README quotes the warning as `server <id> has no local account left; it still asks for a login wherever it did`.
- [ ] No statement in the README says that removing an account opens a server or removes the need for a login.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
