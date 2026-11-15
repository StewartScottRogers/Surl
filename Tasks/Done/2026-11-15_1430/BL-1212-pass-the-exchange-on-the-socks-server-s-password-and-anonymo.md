---
id: BL-1212
title: Pass the exchange on the SOCKS server's password and anonymous logins
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests]
requirement: FR-060
created: 2026-11-15
completed: 2026-11-15
---
# BL-1212 — Pass the exchange on the SOCKS server's password and anonymous logins

## Goal

The SOCKS server's username/password and anonymous logins carry their exchange, so each `LoginJudgedEvent` has the exchange ID and a `refuse-login;listener=socks5://...` rule applies.

## Context

- Found by the stand-in audit of 2026-11-15. `Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/Socks5Exchange.cs` line 171 and `SocksProxyAccounts.cs` line 30 build `PasswordLogin(context.Scheme, ..., client.TlsSession)` without the `Exchange` argument. As a result, `LoginJudgedEvent.ExchangeId` is null and a listener-scoped `refuse-login` never matches. BL-1083 made the same change for other servers.
- The SOCKS5 GSS-API login does not go through the policy at all. BL-1214 decides that case; it is not part of this task.
- Upstream behaviour is unchanged: the bytes sent are the same, so no upstream measurement is needed.

## Acceptance criteria

- [x] Both `PasswordLogin`s pass `LoginExchange.Of(context)`.
- [x] Tests in `Protocols/Proxy/Surl.Protocol.Socks.UnitTests` show the policy receives an `Exchange` whose ID and scheme are the connection's, for a SOCKS5 username/password login and for a SOCKS4 anonymous check.
- [x] `dotnet build Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary -warnaserror` is clean and the fast tests pass.

## Notes

- Both PasswordLogin calls now pass LoginExchange.Of(context). SOCKS5 asks the policy twice (anonymous check, then password), so the SOCKS5 test checks every login seen. No upstream measurement: bytes unchanged.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. SOCKS password and anonymous logins pass their exchange to the policy
