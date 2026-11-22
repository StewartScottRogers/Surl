---
id: BL-1587
title: Mark a NATS auth_token CONNECT as a token login
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1586]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-22
completed:
---
# BL-1587 — Mark a NATS auth_token CONNECT as a token login

## Goal

A NATS `CONNECT` carrying `auth_token` and no `user` reaches the policy as a `PasswordLogin` with `IsToken = true`
(BL-1586), so `surl --user :tok` accepts `CONNECT {"auth_token":"tok"}` over `nats` (with `--allow-plaintext-auth`)
and `natss`.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T0726-Surl-bug-report.md` item 1 (High): every
  token login answers `-ERR 'Authorization Violation'`, log reason `RefusedCredentials`.
- NATS is an extended server, judged by the NATS client protocol specification, "CONNECT" (`auth_token`: "Client
  authorization token"), never by the Curl port (ADR-0083). ADR-0113 decision 2 already decides the behaviour:
  `user`/`pass` log in as that account; otherwise `auth_token` logs in as the empty-name account whose password is
  the token; `auth_token` is ignored when `user` is present. No ADR change.
- Cause: `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary/NatsConnectOptions.cs:97-100`
  (`ToPasswordLogin`) builds `new PasswordLogin(scheme, string.Empty, Encode(AuthToken), ...)` without marking it a
  token, and `Surl.Authentication` refuses an unmarked empty user name (ADR-0032 section 1; see BL-1586).
- Why the tests missed it: `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests/UnitTestNatsAuthenticationPolicy.cs:31`
  accepts `("", "s3cret")` as a plain password login, which the real policy never does. Make the fake judge a
  token only when `IsToken` is set.
- Not a defect, keep as is: `{"user":"","pass":"tok"}` is a `user`/`pass` login with an empty name and stays refused
  (ADR-0032 section 1, ADR-0113 decision 2). How a particular client spells a token on the wire is the client's
  business; the spec's field is `auth_token`.
- Stand-in: no store or event change here. The accepted token's bus account stays the empty name
  (`NatsServerConnection.cs:296-314`, `LogInAsync`), and the `LoginJudgedEvent` comes from the policy (BL-1586).

## Acceptance criteria

- [ ] `NatsConnectOptions.ToPasswordLogin` sets `IsToken = true` for the `auth_token` login and leaves it `false` for
      `user`/`pass` and for no credentials; `NatsConnectOptionsTests` pins all three (extend the DataRows at line 84-86).
- [ ] `UnitTestNatsAuthenticationPolicy` accepts the empty name only when `IsToken` is set; `ServeAsync_AuthToken_LogsInAsTheEmptyNameAccount`
      asserts `IsToken` too.
- [ ] A new fast test in `NatsServerConnectionTests` replays the spec-written bytes
      `CONNECT {"verbose":false,"pedantic":false,"tls_required":false,"auth_token":"tok","name":"","lang":"go","version":"1.2.2","protocol":1}\r\nPING\r\n`
      and expects `PONG\r\n` with no `-ERR`; another sends `{"user":"","pass":"tok"}` and expects
      `-ERR 'Authorization Violation'\r\n`.
- [ ] The same token CONNECT over the TLS fake (`NatsServerConnectionTests.Tls.cs`) is accepted.
- [ ] `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary -warnaserror` clean, fast tests green,
      the library stays at 100% line and branch coverage; no test needs `TestCategory=Integration`.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
