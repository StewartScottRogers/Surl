---
id: BL-1241
title: Name the logged-in account on the gRPC server's command-stage fault point
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests]
requirement: FR-062
created: 2026-11-16
completed:
---
# BL-1241 — Name the logged-in account on the gRPC server's command-stage fault point

## Goal

A `delay`, `drop` or `error` fault rule scoped with `account=<name>` fires on a gRPC call made with that account's Basic or Bearer login, as it already does on the HTTP, SignalR, MCP and Socket.IO servers.

## Context

- Found by the second stand-in audit (2026-11-16). ADR-0124 decision 4 lets every rule be scoped by `account`, and `Surl.Cli`'s `ConfiguredFaultReader` accepts it, so `error;scheme=grpc;account=alice;reply=14 busy` is accepted today and never fires.
- `Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary/GrpcCallFaults.cs` `ApplyAsync(context, path, methodName)` builds `FaultPoint(FaultStage.Command) { ExchangeId, ListenUrl, Scheme, Path, Command }` with no `Account`.
- `GrpcStreamHandler.AuthenticateAsync` (around line 158) judges the call through `IHttpAuthenticationSession.JudgeAsync` and discards the verdict; `HttpAuthenticationVerdict.AccountName` (`Foundation/Surl.Protocol.Abstractions.UnitLibrary/HttpAuthenticationVerdict.cs`) holds the accepted account. Return it from `AuthenticateAsync` and pass it to `GrpcCallFaults.ApplyAsync` (`AnswerAsync`, around line 142), setting `Account = string.IsNullOrEmpty(accountName) ? null : accountName` as `Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpCommandFaults.cs` does.
- An extended server (ADR-0083): judged by its specification; no upstream curl measurement applies, and the reply shape is ADR-0124's `grpc` row (BL-1188), unchanged.
- Stand-in: no store and no new event.

## Acceptance criteria

- [ ] A test in `Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests` shows a call with `alice`'s Basic login under a `ScriptedFaultPlan` error rule with `Account = "alice"` and reply `14 busy` ends Trailers-Only with `grpc-status: 14`, and the recorded point's `Account` is `alice`.
- [ ] A test shows the same rule does not fire for `bob`'s call: it is served.
- [ ] A test shows an unauthenticated call (no accounts configured) leaves the point's `Account` null.
- [ ] `dotnet build Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary -warnaserror` is clean, and the library keeps 100% line and branch coverage.
- [ ] `Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary/CLAUDE.md`, section "Faults", states that the point names the logged-in account.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
