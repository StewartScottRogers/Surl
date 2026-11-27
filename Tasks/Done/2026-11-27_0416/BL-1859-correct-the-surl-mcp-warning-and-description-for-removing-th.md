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
completed: 2026-11-27
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

- [x] The warning, in the `warning` member and the second text block, is
      `server <id> has no local account left; it still asks for a login wherever it did`, pinned by a named test.
- [x] The tool description holds
      `Removing the last account does not open the server: it still asks for a login wherever it did; the result then
      carries a warning, and add_account lets that account in.`, pinned by the test that pins the descriptions.
- [x] The tool's XML document and the library's `CLAUDE.md` say the same; neither says the server needs no login.

## Notes

- Warning pinned by `DeleteAndRemoveToolsTests.RemoveAccount_TheLastAccount_AnswersSuccessWithAWarningThatTheServerStillAsksForALogin`
  (second text block and `warning` member). Description pinned by
  `ProgramTests.RunAsync_ToolsList_RemoveAccountsDescriptionSaysRemovingTheLastAccountDoesNotOpenTheServer`. Both
  failed against the old text before the change.
- No new ADR: ADR-0132 amendment 5, decision 8 fixes both texts.
- `Application/Surl.Mcp/README.md` still quotes the old warning. It is outside `touches` and is BL-1886's, which
  depends on this task.
- Verified: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests green
  (`Surl.Mcp.UnitTests` 463 passed).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. remove_account's warning and description now say removing the last server account does not open the server
