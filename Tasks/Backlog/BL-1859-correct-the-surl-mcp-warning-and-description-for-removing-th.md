---
id: BL-1859
title: Correct the surl-mcp warning and description for removing the last server account
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1863, BL-1853]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1859 — Correct the surl-mcp warning and description for removing the last server account

## Goal

`remove_account` tells an agent what ADR-0132 amendment 5 says removing the last server account leaves.

## Context

- ADR-0132 amendment 5, decision 8.
- `RemoveAccountTool`'s description, XML document and warning say the server needs no login and anyone may connect,
  with `--user-ldap` too. The library's `CLAUDE.md` quotes the warning.
- The proxy accounts' warning (`RemoveProxyAccountTool`) is BL-1853's.

## Acceptance criteria

- [ ] The warning, in the `warning` member and the second text block, is
      `server <id> has no local account left; it still asks for a login wherever it did`, pinned by a named test.
- [ ] The tool description holds
      `Removing the last account does not open the server: it still asks for a login wherever it did; the result then
      carries a warning, and add_account lets that account in.`, pinned by the test that pins the descriptions.
- [ ] The tool's XML document and the library's `CLAUDE.md` say the same; neither says the server needs no login.

## Notes

## Log

- 2026-11-27: Created.
