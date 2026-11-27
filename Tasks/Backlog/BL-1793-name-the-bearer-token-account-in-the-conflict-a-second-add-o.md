---
id: BL-1793
title: Name the Bearer-token account in the conflict a second add of it answers
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Composition.UnitLibrary, Surl.Composition.UnitTests]
requirement: none
created: 2026-11-26
completed:
---
# BL-1793 — Name the Bearer-token account in the conflict a second add of it answers

## Goal

A second add of the Bearer-token account (empty user name) is refused with a message that names the Bearer-token
account, through the control endpoint and through `Surl.Api`, instead of `user  already has an account` with a blank
where the name goes.

## Context

Bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-26T2130-Surl-bug-report.md`, item 1 (Low, against
Surl v0.1.10, commit `654b8d2cb`). Repro, copied from the report:

> `surl --directory site --user app:s3cret --control http://127.0.0.1:18702 --control-token-file ct.txt http://127.0.0.1:18701/`, with `ct.txt` holding the control token.
> - `curl -s -w " %{response_code}\n" --json '{"user":"","password":"t1"}' -H "Authorization: Bearer <control token>" http://127.0.0.1:18702/v1/accounts`, twice.
> - Observed: ` 204`, then `{"error":"conflict","message":"user  already has an account"} 409`. Surl.Api's `Accounts.Add("", t)` a second time throws `User  already has an account.`
> - Expected: the name of the account, as for any other, or surl-mcp's wording: `the Bearer-token account already exists`.

Where the two messages come from:
- `Foundation/Surl.Authentication.UnitLibrary/AccountStore.cs`, `Add`: throws
  `ArgumentException($"User {account.UserName} already has an account.")`. `Surl.Api`'s
  `SurlServerAccounts.Add` (`Application/Surl.Api/SurlServerAccounts.cs`) reaches it through
  `ComposedStores.AddAccount`, so fixing it here fixes `Surl.Api` without touching `Surl.Api`.
- `Application/Surl.Composition.UnitLibrary/ComposedControlTarget.cs`, `AddAccountAsync`: catches that and throws
  `ControlRefusalException(ControlRefusalKind.Conflict, $"user {user} already has an account")`.

`surl-mcp` already says it right: `Application/Surl.Mcp.UnitLibrary/AddAccountTool.cs` answers
`the Bearer-token account already exists` when `user.Length == 0`
(`BearerTokenAccountToolsTests` pins it). Use the same wording, so one concept has one phrase across the three hosts:
`The Bearer-token account already exists.` from `AccountStore` (sentence case, as its other message) and
`the Bearer-token account already exists` from the control endpoint (lower case, as its other refusals). A named
account's messages stay exactly as they are; the 409 status and `conflict` error code do not change.

Stand-in: no store, event or fault changes. The account store keeps the existing account and publishes nothing on a
refused add, as today; only the refusal text changes, so a test asserting the conflict can read which account
clashed.

## Acceptance criteria

- [ ] `AccountStore.Add` of an `Account("", …)` when the Bearer-token account exists throws `ArgumentException` whose
      message is `The Bearer-token account already exists.`; a regression test in `Surl.Authentication.UnitTests`
      (beside the duplicate-name tests in `AccountBookTests.Changes.cs`) pins it, and the named-account message
      `User <name> already has an account.` stays pinned.
- [ ] `ComposedControlTarget.AddAccountAsync("", …)` a second time throws `ControlRefusalException` with
      `ControlRefusalKind.Conflict` and message `the Bearer-token account already exists`; a new test in
      `ComposedControlTargetTests` pins it, and the existing `user alice already has an account` test still passes.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` and
      `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` are clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [ ] Both changed libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-26: Created.
