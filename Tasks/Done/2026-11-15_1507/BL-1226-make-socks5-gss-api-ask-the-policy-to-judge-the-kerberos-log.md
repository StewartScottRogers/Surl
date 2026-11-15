---
id: BL-1226
title: Make SOCKS5 GSS-API ask the policy to judge the Kerberos login (ADR-0165)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1214, BL-1221, BL-1225]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests]
requirement: FR-060
created: 2026-11-15
completed: 2026-11-15
---
# BL-1226 — Make SOCKS5 GSS-API ask the policy to judge the Kerberos login (ADR-0165)

## Goal

Socks5GssApiNegotiation calls the policy after Accept and answers 01 FF when it refuses.

## Context

ADR-0165 (Documentation/Planning/Decisions) decides this. No wire byte changes; existing upstream curl 8.21.0 fixtures stay green.

## Acceptance criteria

- [x] Called with the principal, scheme and LoginExchange.Of(context) before the AP-REP is written; with no policy nothing changes; refused path sends 01 FF and logs "SOCKS5 GSS-API login refused"; Surl.Composition passes the policy to the SOCKS server (add Application/Surl.Composition.* to touches if not already doing so).
- [x] dotnet build is clean and the fast tests pass; the project stays at 100% coverage.

## Notes

Composition already passes `ProxyAuthenticationPolicy` to `SocksProtocolServer`, so no Composition change was needed. A refused token is also judged (IsTokenAccepted false, no principal) per ADR-0165 decision 7.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. SOCKS5 GSS-API judges the Kerberos login through the policy
