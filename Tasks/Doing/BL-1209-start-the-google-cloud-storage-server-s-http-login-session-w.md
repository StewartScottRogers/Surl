---
id: BL-1209
title: Start the Google Cloud Storage server's HTTP login session with its exchange so gs logins are published and refused as gs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests]
requirement: FR-060
created: 2026-11-15
completed:
---
# BL-1209 — Start the Google Cloud Storage server's HTTP login session with its exchange so gs logins are published and refused as gs

## Goal

A Google Cloud Storage login is published as a `LoginJudgedEvent` with scheme `gs` and its exchange ID, and a `refuse-login` rule scoped by `scheme=gs` or its listener refuses it, while one scoped `scheme=http` no longer does.

## Context

- Found by the stand-in audit of 2026-11-15. `Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/GoogleCloudStorageProtocolServer.cs` line 75 calls `authenticationPolicy.StartHttpConnection(connection.TlsSession)` without the exchange. Bearer logins are therefore judged and published as `http` with no exchange ID (`Foundation/Surl.Authentication.UnitLibrary/HttpAuthenticationSession.cs` lines 219-237). BL-1207 describes the same defect in S3.
- Fix: `StartHttpConnection(connection.TlsSession, LoginExchange.Of(context))`. `UnitTestGcsAuthenticationPolicy.cs` in the tests implements only the one-argument overload; override the two-argument member to record the exchange.

## Acceptance criteria

- [ ] `GoogleCloudStorageProtocolServer` passes `LoginExchange.Of(context)`, and no one-argument `StartHttpConnection` call remains in the library.
- [ ] A test in `Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests` shows the policy receives an exchange whose `Scheme` is `gs` and whose `ExchangeId` is the context's.
- [ ] A test with the real `AuthenticationPolicy`, a recording publisher and `refuse-login;scheme=gs` shows an accepted bearer token is refused as a wrong one is, with a `LoginJudgedEvent` whose scheme is `gs`.
- [ ] `dotnet build Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
