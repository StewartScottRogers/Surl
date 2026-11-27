---
id: BL-1879
title: Correct the surl-mcp host README for removing the last server account
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1859]
touches: [Application/Surl.Mcp/README.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1879 — Correct the surl-mcp host README for removing the last server account

## Goal

`Application/Surl.Mcp/README.md` quotes the warning `remove_account` gives for the last server account as the code
gives it once BL-1859 is done.

## Context

- Found by BL-1853. ADR-0132 amendment 5, decision 8 and consequence 5.
- The README's bullet for `remove_account` quotes `server <id> has no account left and now needs no login`. BL-1859
  changes that text but touches only `Surl.Mcp.UnitLibrary` and `Surl.Mcp.UnitTests`.

## Acceptance criteria

- [ ] The `remove_account` bullet of `Application/Surl.Mcp/README.md` quotes
      `server <id> has no local account left; it still asks for a login wherever it did`.
- [ ] `Application/Surl.Mcp/README.md` holds no text saying a server with its last account removed needs no login.

## Notes

## Log

- 2026-11-27: Created.
