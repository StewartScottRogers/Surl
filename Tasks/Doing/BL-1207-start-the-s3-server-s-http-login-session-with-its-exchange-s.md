---
id: BL-1207
title: Start the S3 server's HTTP login session with its exchange so s3 logins are published and refused as s3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: FR-060
created: 2026-11-15
completed:
---
# BL-1207 — Start the S3 server's HTTP login session with its exchange so s3 logins are published and refused as s3

## Goal

An S3 login is published as a `LoginJudgedEvent` with scheme `s3` and its exchange ID, and a `refuse-login` rule scoped by `scheme=s3` or `listener=s3://...` refuses it, while one scoped `scheme=http` no longer does.

## Context

- Found by the stand-in audit of 2026-11-15. `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary/S3ProtocolServer.cs` line 78 calls `authenticationPolicy.StartHttpConnection(connection.TlsSession)`, the overload without the exchange. `HttpAuthenticationSession` then judges and publishes with `AuthenticationPolicy.HttpLoginScheme` (`http`) and no exchange (`Foundation/Surl.Authentication.UnitLibrary/HttpAuthenticationSession.cs` lines 219-237, `AuthenticationPolicy.IsRefusedByFaultAsync` lines 255-273). The result:
  - `SurlEventQuery { Scheme = "s3" }` never finds an S3 login;
  - `refuse-login;scheme=s3` and `refuse-login;listener=s3://...` never fire;
  - `refuse-login;scheme=http` refuses S3 logins.
- BL-1082 moved the servers sharing `Surl.HttpServing` to `StartHttpConnection(tlsSession, LoginExchange.Of(context))` (BL-871, ADR-0128 Amendment 1). The extended object-storage servers build their own `Http1ConnectionServer` and were not covered.
- Fix: pass `LoginExchange.Of(context)`. The unit-test policy `UnitTestS3AuthenticationPolicy.cs` implements only the one-argument overload; override the two-argument default interface member there to record the exchange.

## Acceptance criteria

- [ ] `S3ProtocolServer` starts its session with `StartHttpConnection(connection.TlsSession, LoginExchange.Of(context))`, and no one-argument call remains in the library.
- [ ] A test in `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests` shows the policy receives an exchange whose `Scheme` is `s3` and whose `ExchangeId` is the context's.
- [ ] A test with the real `AuthenticationPolicy`, a recording publisher and a `FaultPlan` holding `refuse-login;scheme=s3` shows a correctly signed request is answered as a wrong signature is, with a `LoginJudgedEvent` whose scheme is `s3` and `IsRefusedByFault` true.
- [ ] `dotnet build Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
