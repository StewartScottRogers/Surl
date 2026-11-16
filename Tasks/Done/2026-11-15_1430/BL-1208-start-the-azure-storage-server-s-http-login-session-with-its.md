---
id: BL-1208
title: Start the Azure Storage server's HTTP login session with its exchange so az, abfs and abfss logins are published and refused by their scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: FR-060
created: 2026-11-15
completed: 2026-11-15
---
# BL-1208 — Start the Azure Storage server's HTTP login session with its exchange so az, abfs and abfss logins are published and refused by their scheme

## Goal

An Azure Storage login is published as a `LoginJudgedEvent` with its own scheme (`az`, `abfs` or `abfss`) and exchange ID, and a `refuse-login` rule scoped by that scheme or listener refuses it, while one scoped `scheme=http` no longer does.

## Context

- Found by the stand-in audit of 2026-11-15. `Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary/AzureStorageProtocolServer.cs` line 111 calls `authenticationPolicy.StartHttpConnection(connection.TlsSession)` without the exchange. Logins are therefore judged and published as `http` with no exchange ID (`Foundation/Surl.Authentication.UnitLibrary/HttpAuthenticationSession.cs` lines 219-237). BL-1207 describes the same defect in S3.
- Fix: `StartHttpConnection(connection.TlsSession, LoginExchange.Of(context))`. `UnitTestBearerAuthenticationPolicy.cs` in the tests implements only the one-argument overload; override the two-argument member to record the exchange.

## Acceptance criteria

- [x] `AzureStorageProtocolServer` passes `LoginExchange.Of(context)`, and no one-argument `StartHttpConnection` call remains in the library.
- [x] A test in `Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests` shows the policy receives an exchange whose `Scheme` is the listen URL's (`az` in one case, `abfs` in another) and whose `ExchangeId` is the context's.
- [x] A test with the real `AuthenticationPolicy`, a recording publisher and `refuse-login;scheme=az` shows a correctly signed Shared Key request is refused as a wrong key is, with a `LoginJudgedEvent` whose scheme is `az`.
- [x] `dotnet build Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary -warnaserror` is clean and the fast tests pass.

## Notes

- Fixed by passing `LoginExchange.Of(context)` at the one call. Added a test-project reference to `Surl.Authentication.UnitLibrary` (inside `touches`) for the real-policy test; the library gains none.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Azure Storage logins are published and refused under az, abfs or abfss with the exchange ID
