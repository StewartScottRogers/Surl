---
id: BL-1604
title: Prove Surl.Api rotates the Bearer-token account while the server runs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1601]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-22
completed:
---
# BL-1604 — Prove Surl.Api rotates the Bearer-token account while the server runs

## Goal

Tests in `Surl.Api.UnitTests` prove that a test can expire a Bearer token mid-session in-process:
`SurlServer.Accounts.SetPassword("", "tok456")` on a server started with `--user :tok123` makes the next request
with `Bearer tok123` refused and `Bearer tok456` accepted, and `Accounts.Add("", ...)` / `Accounts.Remove("")` add and
remove the token account.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T1308-Surl-bug-report.md` item 2 (recipes
  `http-expire-a-token-mid-session*`). BL-1601 records the rule for every host; this task pins the in-process host.
- Today's code appears to allow it already: `Application/Surl.Api/SurlServerAccounts.cs:33` (`Add`) refuses only a
  `:`, `SetPassword` (same file) refuses only an empty password or an unknown name, and
  `Foundation/Surl.Authentication.UnitLibrary/AccountBook.cs` documents `Remove` and `SetPassword`'s `userName` as
  "the empty name is the Bearer token's account". No test pins it. If a test shows otherwise, fix it in
  `Application/Surl.Api` to what ADR-0132 Amendment 2 says; anything outside `Surl.Api` becomes a new task.
- Existing patterns: `Application/Surl.Api.UnitTests/SurlServerTests.AccountsAndTlsSrpSetPassword.cs` and
  `SurlServerTests.AccountsOpen.cs`.
- Stand-in: the shared account book; the rotation publishes `AccountRemoved` then `AccountAdded` with the empty name
  (ADR-0132 amendment 1.3) and no event holds the token. The test drives HTTP over the in-memory or loopback seam the
  existing `SurlServerTests` use, on port 0.

## Acceptance criteria

- [ ] `SetPassword_EmptyUserOnABearerTokenServer_RefusesTheOldTokenAndAcceptsTheNew` passes (HTTP `Authorization:
      Bearer`; the old token answered `401`, the new one served).
- [ ] `SetPassword_EmptyUser_PublishesAccountRemovedThenAccountAddedWithTheEmptyName` passes and asserts no event text
      holds either token.
- [ ] `Add_EmptyUserWhileNoTokenAccount_AcceptsTheBearerToken` and `Remove_EmptyUser_RefusesTheBearerTokenAfterwards`
      pass.
- [ ] `dotnet build Application/Surl.Api -warnaserror` clean and `dotnet test --filter "TestCategory!=Integration"` green;
      no test needs `TestCategory=Integration`.

## Notes

## Log

- 2026-11-22: Created.
