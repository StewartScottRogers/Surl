---
id: BL-1397
title: Refuse a gRPC call with no login when no account is configured and --allow-anonymous is off
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests]
requirement: none
created: 2026-11-19
completed:
---
# BL-1397 — Refuse a gRPC call with no login when no account is configured and --allow-anonymous is off

## Goal

`surl grpc://127.0.0.1:0/` with no `--user`, no `--user-file` and no `--allow-anonymous` answers a call that carries
no `authorization` with Trailers-Only `UNAUTHENTICATED` (16), as it already does once an account exists, instead of
serving it.

## Context

- Surly cookbook bug report 2026-11-19 09:30, finding 1 (High), reproduced at commit 71807f05f: with no account and
  no `--allow-anonymous`, `curl -d '{"message":"hello"}' --grpc-proto echo.proto grpc://127.0.0.1:50051/surl.test.Echo/Echo`
  exits 0 with the echo. Cookbook review `Z:\repos\Surly\Book\parts\05-new-protocols\chapters\03-remote-calls-and-streams\recipes\grpc-call-a-method\review.md`
  finding 3. (The report's client is the Curl port: it located the defect and is not the oracle. ADR-0116 and
  ADR-0032 are.)
- Cause: `Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary/GrpcStreamHandler.cs:167` judges every call with
  `new HttpAuthenticationRequest("POST", head.Target, IsWrite: false, head.Fields)`, and
  `Foundation/Surl.Authentication.UnitLibrary/HttpAuthenticationSession.cs:180`
  (`return policy.Settings.Accounts.HasAccounts || request.IsWrite ? Challenge() : ProceedAnonymously;`) lets a
  non-write proceed anonymously when no account exists. That is ADR-0032 section 4's rule for HTTP `GET`/`HEAD`
  only: ADR-0032's Context item 1 says that with no accounts "every protocol that has a login refuses every login",
  and item 2 limits anonymous reads to HTTP, Gopher and TFTP. ADR-0116 decision 7 is headed "Secure by default",
  says `--allow-anonymous` is what serves calls without `authorization`, and its Consequences say the server runs
  "with `--allow-anonymous`, or an account". `surl --aihelp grpc` (`AiHelpProse.cs:137`) already says "without an
  account a login is refused unless `--allow-anonymous` is given", so the help is right and the code is wrong.
- Precedent: BL-1091 fixed the same defect in SignalR by judging every request `IsWrite: true`
  (`Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary/SignalRRequestAnswerer.cs:55`, commit 6b1fc29be);
  Socket.IO answers `Not authorized` in the same setup.
- The fix is the one argument: judge every call, reflection included, with `IsWrite: true`. `--allow-anonymous`
  still serves (the session's step 1), and an account's `Basic`/`Bearer` login is judged as before.
- Tests: `Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests/GrpcStreamHandlerTests.cs:441` pins
  `IsWrite` `false` today and must be changed; `UnitTestHttpAuthenticationSession` records `JudgedRequests`.
- Stand-in: no store, event or fault change. A test that wants an open gRPC stand-in passes `--allow-anonymous`
  (or `AllowAnonymous` through `Surl.Api`), which every `AiHelpExamples` gRPC entry (`AiHelpExamples.cs:181`) and
  every Conformance gRPC test already does; the refusal lands before the service lookup, so an unauthenticated
  client learns nothing about what is served (ADR-0116 decision 3).

## Acceptance criteria

- [ ] A failing test first, then the fix: a `GrpcStreamHandlerTests` test shows every call (an `Echo` call and a
      reflection `ServerReflectionInfo` call) is judged with `IsWrite: true`, and the test at line 441 expects
      `true`.
- [ ] A `GrpcStreamHandlerTests` test with a session that answers `Challenge` for a request with no
      `authorization` (as `HttpAuthenticationSession` does with no account once `IsWrite` is true) gets
      Trailers-Only `grpc-status: 16`, `grpc-message: unauthenticated`, and the `Echo` service is never reached.
- [ ] A `GrpcStreamHandlerTests` test with a session that proceeds anonymously (as under `--allow-anonymous`) still
      serves the call.
- [ ] `dotnet build Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Extended.Protocol.Grpc.UnitLibrary`
      stays at 100% line and branch coverage. No test needs `TestCategory=Integration`.

## Notes

- ADR-0116 decision 7's text still names `IsWrite: false`; BL-1409 aligns the ADR afterwards. Do not edit the ADR
  here.
- `Conformance/Surl.Conformance.UnitTests/CurlPortClientCallsMethodsOnSurlOverGrpcTests.cs` (Integration) starts
  every open server with `--allow-anonymous` and needs no change.

## Log

- 2026-11-19: Created.
