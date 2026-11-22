---
id: BL-1602
title: Let the control endpoint add, rotate and remove the Bearer-token account as BL-1601 decides
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1601]
touches: [Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests]
requirement: none
created: 2026-11-22
completed:
---
# BL-1602 — Let the control endpoint add, rotate and remove the Bearer-token account as BL-1601 decides

## Goal

With `surl --user :tok123 --control http://127.0.0.1:0 --control-token-file t https://127.0.0.1:0/`, a test can add,
change the token of and remove the empty-name (Bearer-token) account through the control endpoint, with the routes and
answers ADR-0132 Amendment 2 (BL-1601) names.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T1308-Surl-bug-report.md` item 2;
  BL-1601 decides the spelling. The criteria below assume BL-1601's option (a): `POST /v1/accounts` with `"user": ""`,
  and `PUT` / `DELETE` of `/v1/accounts/` with an empty last segment, for the account book only. If BL-1601 chose
  otherwise it rewrites these criteria before this task is claimed; follow the ADR when the two differ.
- Where the refusals are today:
  - `Application/Surl.Control.UnitLibrary/ControlStoreRequests.cs:220` - `AddAccountToAsync` refuses
    `string.IsNullOrEmpty(body.User)` with `400 give user and password, neither empty`. It is shared with the proxy and
    TLS-SRP adds (`AddProxyAccountAsync`, `AddTlsSrpAccountAsync`), so the empty-user acceptance must apply to the
    account book's `AddAccountAsync` only, unless the ADR says otherwise.
  - `Application/Surl.Control.UnitLibrary/ControlEndpoint.cs:124-125` - the `["accounts", "{}"]` `PUT` and `DELETE`
    routes; the route matcher does not match an empty segment, so `DELETE /v1/accounts/` is `404`.
- `IControlTarget.AddAccountAsync`, `SetAccountPasswordAsync` and `RemoveAccountAsync` already pass the empty name to
  `ComposedStores` and the `AccountBook`, which accept it; no `Surl.Composition` change is needed.
- Stand-in: the shared account book; the add publishes `AccountAdded`, the rotation `AccountRemoved` then
  `AccountAdded`, and the removal `AccountRemoved`, each with the empty name and never the token (ADR-0132 decisions
  6 and amendment 1.3). Tests use `FakeControlTarget` (`Application/Surl.Control.UnitTests/FakeControlTarget.cs`).

## Acceptance criteria

- [ ] In `Application/Surl.Control.UnitTests/ControlStoreRequestsTests.cs`:
      `AddAccount_EmptyUserWithPassword_AddsTheBearerTokenAccount` (`204`, the target saw user `""`),
      `SetAccountPassword_EmptyUser_RotatesTheBearerToken` (`204`; `404` when the target has no such account),
      `RemoveAccount_EmptyUser_RemovesTheBearerTokenAccount` (`204`), all pass.
- [ ] `AddAccount_EmptyPassword_IsBadRequest` and `AddProxyAccount_EmptyUser_IsBadRequest` (and the TLS-SRP twin) pass:
      an empty password is still `400`, and the proxy and TLS-SRP kinds answer an empty user as the ADR states.
- [ ] A route test in `Application/Surl.Control.UnitTests/ControlEndpointTests.cs` shows the empty-name routes
      reach the handlers and every other route with an empty segment (for example `/v1/files/`) is still `404`.
- [ ] No answer body or log line holds the token (asserted in the add and rotate tests).
- [ ] `Application/Surl.Control.UnitLibrary/CLAUDE.md`'s account routes bullet names the Bearer-token form.
- [ ] `dotnet build Application/Surl.Control.UnitLibrary -warnaserror` clean, `dotnet test --filter "TestCategory!=Integration"`
      green, `Surl.Control.UnitLibrary` at 100% line and branch coverage.

## Notes

## Log

- 2026-11-22: Created.
