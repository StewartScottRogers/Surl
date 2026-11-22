---
id: BL-1586
title: Judge a token password login against the empty-name account
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-1586 — Judge a token password login against the empty-name account

## Goal

A `PasswordLogin` a server marks as a token (`IsToken`) is accepted by `AuthenticationPolicy.CheckPasswordLoginAsync`
when its password is the token of the empty-name account (`--user :tok`), so the NATS `auth_token` and Socket.IO
`token` logins ADR-0113 decision 2 and ADR-0100 decision 4.1 promise can succeed at all.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T0726-Surl-bug-report.md` item 1 (High):
  `surl --user :tok --allow-plaintext-auth nats://...` refuses every token login (`RefusedCredentials`), plain and
  TLS; `--user alice:pw` works.
- The cause, in today's code: `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary/NatsConnectOptions.cs:99`
  hands `auth_token` to the policy as `new PasswordLogin(scheme, string.Empty, token, ...)`.
  `Foundation/Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs:732` (`JudgePasswordLogin`) then calls
  `settings.Accounts.CheckPassword(login.UserName, ...)`, and `Foundation/Surl.Authentication.UnitLibrary/AccountBook.cs:237-238`
  turns the empty name into `null` so it never matches. That is on purpose: ADR-0032 section 1 says "an empty-name
  account is never matched by any other method" than a Bearer token. So the empty name alone must not become a token
  (an MQTT, FTP or STOMP client sending an empty user name would then log in with the token); the server has to say
  it is sending a token.
- `Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoConnectCredentials.cs:37` has the same defect for
  Socket.IO's `token` (fixed by its own task once this lands).
- `AccountBook.CheckBearerToken` (`AccountBook.cs:245`) is the existing check against the empty-name account's
  password; use it, do not add a second comparison.
- Stand-in: no store or event kind is added. The login publishes the `LoginJudgedEvent` ADR-0128 decision 2 already
  describes (Mechanism `password` for `CheckPasswordLoginAsync`, Account `bearer token` for a bearer token), so a test
  sees a token login on the event stream and a `refuse_login` fault rule scoped to the empty-name account refuses it
  (`IsRefusedByFaultAsync` with account `""`), as for every other checked login.

## Acceptance criteria

- [x] `Foundation/Surl.Protocol.Abstractions.UnitLibrary/PasswordLogin.cs` gains `public bool IsToken { get; init; }`
      (default `false`, XML-documented: the password is a token for the empty-name account, ADR-0100 decision 4.1); no
      other Abstractions change.
- [x] In `AuthenticationPolicy.JudgePasswordLogin`, an `IsToken` login is checked with `settings.Accounts.CheckBearerToken`
      after the same `--allow-anonymous` and plain-text rules as any password login (`RefusedPlaintext` on an
      unencrypted connection without `--allow-plaintext-auth`); a login without `IsToken` and an empty user name is still
      refused, as today.
- [x] New fast tests in `Foundation/Surl.Authentication.UnitTests` (e.g. `PasswordLoginTokenTests`):
      `CheckPasswordLoginAsync_TokenOfTheEmptyNameAccount_IsAccepted`, `..._WrongToken_IsRefusedCredentials`,
      `..._TokenOverPlainTextWithoutAllowPlaintextAuth_IsRefusedPlaintext`, `..._EmptyUserNameWithoutIsToken_IsRefusedCredentials`,
      `..._TokenWhenOnlyNamedAccountsExist_IsRefusedCredentials`.
- [x] A test pins the published `LoginJudgedEvent` for an accepted and a refused token login (Account `bearer token`,
      Mechanism `password`), and one shows a `RefuseLogin` fault rule for account `""` refusing an otherwise accepted token.
- [x] `Foundation/Surl.Authentication.UnitLibrary/CLAUDE.md` states the token login in one bullet.
- [x] `dotnet build -warnaserror` clean, `dotnet test --filter "TestCategory!=Integration"` green, and
      `Surl.Authentication.UnitLibrary` stays at 100% line and branch coverage.

## Notes

The Account `bearer token` in the event follows ADR-0128 decision 2 ("`bearer token` for a bearer token"); the
fault point's account stays the matched account name, the empty string, as `ConcludeCheckedLoginAsync` passes it.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. A PasswordLogin with IsToken is judged against the empty-name account's token; tests added
