---
id: BL-1603
title: Let surl-mcp's account tools add, rotate and remove the Bearer-token account as BL-1601 decides
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1601]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-22
completed:
---
# BL-1603 — Let surl-mcp's account tools add, rotate and remove the Bearer-token account as BL-1601 decides

## Goal

On a server started with `--user :tok123`, the `surl-mcp` tools `add_account`, `set_account_password` and
`remove_account` accept the empty user name ADR-0132 Amendment 2 (BL-1601) allows, so an agent can add, rotate and
remove the Bearer-token account; the proxy and TLS-SRP tools keep their `minLength` 1 unless the ADR says otherwise.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T1308-Surl-bug-report.md` item 2: `add_account`
  `{"user":"","password":"tok456"}` fails with `McpProtocolException` (schema validation, ADR-0103's `minLength`).
  BL-1601 decided option (b) (ADR-0132 Amendment 2): `"user": ""` on the three tools, and the ADR's table gives the texts (`the Bearer-token account already exists`, `the Bearer-token account does not exist`, `server <id> has no Bearer-token account`). If it chose otherwise it rewrites
  them before this task is claimed.
- Where: `"minLength": 1` on `user` in `Application/Surl.Mcp.UnitLibrary/AddAccountTool.cs:27`,
  `SetAccountPasswordTool.cs:27` and `RemoveAccountTool.cs:28`. `AddAccountTool.Add` (line 43) maps every
  `ArgumentException` to `user <name> already has an account`; with an empty name the text must still read sensibly
  (the ADR's table gives it). `RemoveAccountTool`'s description (line 19) and the result's warning when the last
  account goes stay as they are.
- `SurlServer.Accounts.Add`, `SetPassword` and `Remove` (`Application/Surl.Api/SurlServerAccounts.cs`) already take
  the empty name (BL-1604 proves it); no `Surl.Api` change belongs here.
- Stand-in: the shared account book; the events are `AccountAdded` / `AccountRemoved` with the empty name, never the
  token. Results are `{"user": ""}`; the token is never echoed (ADR-0103 decision 4).

## Acceptance criteria

- [ ] The three tools' input schemas allow an empty `user`; `password` keeps `minLength` 1 on all of them.
- [ ] New tests (in the test file that holds the existing `add_account` tests, or a new
      `Application/Surl.Mcp.UnitTests/BearerTokenAccountToolsTests.cs`):
      `AddAccount_EmptyUser_AddsTheBearerTokenAccountAndAnswersTheEmptyUser`,
      `SetAccountPassword_EmptyUser_RotatesTheBearerToken`,
      `RemoveAccount_EmptyUser_RemovesTheBearerTokenAccount`,
      `AddAccount_EmptyUserTwice_IsAToolErrorThatHoldsNoToken`.
- [ ] A test shows a Bearer login with the old token is refused and with the new one accepted after
      `set_account_password` (through the started server's authentication policy, no network).
- [ ] `ProxyAndTlsSrpAccountToolsTests` still pass unchanged (their `user` keeps `minLength` 1).
- [ ] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` clean, `dotnet test --filter "TestCategory!=Integration"`
      green, `Surl.Mcp.UnitLibrary` at 100% line and branch coverage.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
