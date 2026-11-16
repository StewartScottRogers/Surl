---
id: BL-1240
title: Name the logged-in account on the SOCKS5 server's command-stage fault point
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests]
requirement: FR-062
created: 2026-11-16
completed: 2026-11-16
---
# BL-1240 — Name the logged-in account on the SOCKS5 server's command-stage fault point

## Goal

A `delay`, `drop` or `error` fault rule scoped with `account=<name>` fires on a SOCKS5 `CONNECT` made by a client logged in as that account, as it already does on every other server with logins.

## Context

- Found by the second stand-in audit (2026-11-16). ADR-0124 decision 4 lets every rule be scoped by `account`, and `Surl.Cli`'s `ConfiguredFaultReader` accepts `account` on `delay`, `drop` and `error` (`ScopeKeys`), so `error;scheme=socks5;account=alice;reply=2` is accepted today and never fires.
- `Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/SocksCommandFaults.cs` `FindAsync(context, client, target)` builds `FaultPoint(FaultStage.Command) { ExchangeId, ListenUrl, Scheme, Path = target, Command = CONNECT }` with no `Account`. `FaultPlan.MatchesScope` (`Foundation/Surl.Core.UnitLibrary/FaultPlan.cs`) needs the point's account to match a rule's.
- The account is known before the request: `Socks5Exchange.JudgeLoginAsync(userName, password, ...)` (RFC 1929 login accepted by the policy) and `Socks5GssApiNegotiation.JudgeLoginAsync` (the Kerberos client principal, `securityContext.ClientPrincipal.ToString()`, as `LoginJudgedEvent` already publishes it, ADR-0165). Carry the accepted name to `Socks5Exchange` line 226's `SocksCommandFaults.FindAsync` call. An anonymous session (no accounts, or `--allow-anonymous`) and SOCKS4's `USERID` (not an account) leave `Account` null.
- Pattern to copy: `Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary/ProxyCommandFault.cs` sets `Account = string.IsNullOrEmpty(accountName) ? null : accountName`.
- No byte changes for an unscoped rule; a scoped rule now answers with the reply ADR-0124's SOCKS row already pins (BL-1061: `05 <code>`), so no new measurement is needed.
- Stand-in: no store and no new event; the existing `FaultAppliedEvent` is published once per application, as today.

## Acceptance criteria

- [x] A test in `Protocols/Proxy/Surl.Protocol.Socks.UnitTests` shows a SOCKS5 client logged in with RFC 1929 as `alice` gets `05 02` for a `CONNECT` under `error;account=alice;reply=2` (a `ScriptedFaultPlan` rule with `Account = "alice"`), and `ScriptedFaultPlan`'s recorded point has `Account` `alice`.
- [x] A test shows the same rule does not fire for a client logged in as `bob`: the `CONNECT` is granted.
- [x] A test shows a GSS-API login's point carries the client principal as `Account`.
- [x] A test shows an anonymous SOCKS5 session and a SOCKS4 request leave the point's `Account` null.
- [x] `dotnet build Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary -warnaserror` is clean, and the library keeps 100% line and branch coverage.
- [x] `Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/CLAUDE.md` states that the command-stage point names the logged-in account.

## Notes

GSS-API: Socks5GssApiNegotiation exposes Account (client principal) once accepted; Socks5Exchange keeps it with the RFC 1929 name.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. SOCKS5 command-stage fault point names the logged-in account
