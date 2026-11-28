---
id: BL-2023
title: Say in the McpAuthorizationGrants comments that a token of no named account is refused
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary/McpAuthorizationGrants.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2023 — Say in the McpAuthorizationGrants comments that a token of no named account is refused

## Goal

The XML comments of `McpAuthorizationGrants.AccountOf` and `RedeemRefreshToken` name every case in which they return
`null` for a token's account.

## Context

Found while working BL-2005 (2026-11-27), read from source. `///` comment text only; no code or behaviour change.

The file is `Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary/McpAuthorizationGrants.cs`.

- `AccountOf`: `<returns>` says "The account, or `null` when the token is not live or is bound to another resource." It
  also returns `null` when `accounts.NamesAccount(token.Account)` is `false`, that is, when the token's account is not
  a named account (ADR-0167 decision 4).
- `RedeemRefreshToken`: `<returns>` says "of an account since removed", and the `accounts` parameter says "a token of
  an account it no longer names". The same `NamesAccount` check is also `false` for a name that never was in the
  account book: `McpPasswordCheck` gives the name of a user only the `--user-ldap` directory knows, and
  `AuthenticationPolicy.NamesAccount` (`Surl.Authentication`) asks the account book alone.

Describe what the code does when this task runs: BL-2003 decides how a directory user's token is judged, and BL-2004
or a task BL-2003 files may change the answer or this member's caller. No store, event or fault changes: documentation
only.

## Acceptance criteria

- [x] The `AccountOf` `<returns>` names three `null` cases: a token that is not live, one bound to another resource, and one whose account the policy does not name.
- [x] The `RedeemRefreshToken` `<returns>` and the `accounts` parameter comment do not limit the account refusal to an account that was removed, unless the code then does.
- [x] The diff changes only `///` lines in `McpAuthorizationGrants.cs`.
- [x] `dotnet build Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary -warnaserror` is clean.

## Notes

- BL-2020 changed the code after this task was filed: both members now ask `IAuthenticationPolicy.HonoursTokenIssuedTo`,
  not `NamesAccount`. As Context asks, the comments describe that code. The third `null` case reads "the policy does not
  honour a token issued to its account (by default, an account the policy does not name)": "does not name" alone would be
  false, because `AuthenticationPolicy` also honours a recently accepted directory user's token.
- "No longer honours" became "does not honour": a refused token need not ever have been honoured (for example a directory
  login for a name the account book once held, `AuthenticationPolicy.HonoursTokenIssuedTo`).
- The `accounts` parameter comment now says the default refuses a removed account's token and that of a name never named.
- Three comment edits made in the lane session, not delegated to `align-and-document`: the code to read was already
  loaded. No ADR: no decision, no behaviour change.
- Verify: solution and project builds with `-warnaserror` clean (0 warnings), `dotnet format --verify-no-changes`
  passed, fast tests 30640 passed, 0 failed, 3 skipped.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The McpAuthorizationGrants comments name every null case, including a token of an account the policy does not honour
