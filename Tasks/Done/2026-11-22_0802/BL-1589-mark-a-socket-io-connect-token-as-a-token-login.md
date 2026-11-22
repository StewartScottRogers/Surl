---
id: BL-1589
title: Mark a Socket.IO CONNECT token as a token login
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1586]
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-1589 — Mark a Socket.IO CONNECT token as a token login

## Goal

A Socket.IO CONNECT payload carrying a string `token` reaches the policy as a `PasswordLogin` with `IsToken = true`
(BL-1586), so `surl --user :tok socketio://...` accepts `{"token":"tok"}` as ADR-0120 decision 4 and ADR-0100
decision 4.1 say.

## Context

- Found while planning the Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T0726-Surl-bug-report.md`
  item 1 (the NATS token): the same defect sits in
  `Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoConnectCredentials.cs:37`, which builds
  `new PasswordLogin(scheme, string.Empty, token, ...)` unmarked, and `Surl.Authentication` refuses an unmarked empty
  user name (ADR-0032 section 1).
- Socket.IO is an extended server, judged by the Socket.IO protocol ("Connection to a namespace"), never by the Curl
  port (ADR-0083).
- The current tests (`SocketIoConnectCredentialsTests.cs:15-17`) check only the user name and password, so they pass.
- Stand-in: no store or event change; the `LoginJudgedEvent` comes from the policy (BL-1586).

## Acceptance criteria

- [x] `SocketIoConnectCredentials.Read` sets `IsToken = true` for the `token` login and `false` for `username`/`password`
      and for none; `SocketIoConnectCredentialsTests` pins `IsToken` in each DataRow.
- [x] Any Socket.IO test fake policy that accepts the empty name accepts it only when `IsToken` is set.
- [x] `dotnet build Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary -warnaserror` clean, fast tests green, the
      library stays at 100% line and branch coverage; no test needs `TestCategory=Integration`.

## Notes

- Fake policy `UnitTestPasswordPolicy` now accepts the empty name only when `IsToken` is set; session test pins `IsToken = true`.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. Token login marked IsToken; tests pin it
