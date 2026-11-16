---
id: BL-1242
title: Name the logged-in account on the WebTransport server's command-stage fault point
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: FR-062
created: 2026-11-16
completed:
---
# BL-1242 — Name the logged-in account on the WebTransport server's command-stage fault point

## Goal

A `delay`, `drop` or `error` fault rule scoped with `account=<name>` fires on a WebTransport extended `CONNECT` made with that account's login, as it already does on the HTTP, SignalR, MCP and Socket.IO servers.

## Context

- Found by the second stand-in audit (2026-11-16). ADR-0124 decision 4 lets every rule be scoped by `account`, and `Surl.Cli`'s `ConfiguredFaultReader` accepts it, so `error;scheme=webtransport;account=alice;reply=503` is accepted today and never fires.
- `Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary/WebTransportConnectFaults.cs` `FindAsync(context, path)` builds `FaultPoint(FaultStage.Command) { ExchangeId, ListenUrl, Scheme, Path, Command = CONNECT }` with no `Account`.
- `WebTransportConnectAuthentication.RefuseAsync` judges the CONNECT through `IHttpAuthenticationSession.JudgeAsync` and returns only the refusal fields; the verdict's `HttpAuthenticationVerdict.AccountName` is dropped. `WebTransportConnectHandler.JudgeAsync` (around line 133) runs it before `WebTransportConnectFaults.FindAsync` (around line 107). Carry the accepted account name from the verdict to `FindAsync`, setting `Account = string.IsNullOrEmpty(accountName) ? null : accountName` as `Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpCommandFaults.cs` does.
- An extended server (ADR-0083): judged by its specification; no upstream curl measurement applies, and the reply shape is ADR-0124's `webtransport` row (BL-1065), unchanged.
- Stand-in: no store and no new event.

## Acceptance criteria

- [ ] A test in `Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests` shows a CONNECT with `alice`'s Basic login under a `ScriptedFaultPlan` error rule with `Account = "alice"` and reply `503` is answered `:status 503`, and the recorded point's `Account` is `alice`.
- [ ] A test shows the same rule does not fire for `bob`'s CONNECT: it is answered `200`.
- [ ] A test shows a CONNECT with no login (no accounts configured) leaves the point's `Account` null.
- [ ] `dotnet build Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary -warnaserror` is clean, and the library keeps 100% line and branch coverage.
- [ ] `Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary/CLAUDE.md`, paragraph "Chosen-error fault", states that the point names the logged-in account.

## Notes

## Log

- 2026-11-16: Created.
