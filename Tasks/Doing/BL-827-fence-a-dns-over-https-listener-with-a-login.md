---
id: BL-827
title: Fence a DNS over HTTPS listener with a login
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-483, BL-1018]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-827 — Fence a DNS over HTTPS listener with a login

## Goal

With `--user` or `--user-file` given, every DNS over HTTPS request must pass `IHttpAuthenticationSession` with
ADR-0032's rules and the `--auth` methods before it is answered; with no account configured, DoH asks for none.

## Context

- Decision: ADR-0092 decision 5, the "Logins" bullet (ADR-0084 decision 4).
- `DnsOverHttpsHandler` (BL-483) answers every request without a login today. `Surl.Authentication`'s
  `HttpAuthenticationSession` is how the HTTP-based extended servers (McpServer, SignalR, S3) check one; follow theirs.
- The accounts reach the handler from the composition, which BL-485 builds for `dns`.

## Acceptance criteria

- [ ] A named test with an account configured answers a DoH `GET` without credentials `401` with the challenge the
      configured `--auth` methods give, and the same request with Basic credentials `200`.
- [ ] A named test with no account configured answers the RFC 8484 example `GET` `200` with no `Authorization`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-483, which built DoH without the optional login to keep to its acceptance criteria.

## Log

- 2026-11-05: Created.
- 2026-11-06: depends-on BL-485 replaced by BL-1018 (BL-485 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-12: Backlog -> Doing.
