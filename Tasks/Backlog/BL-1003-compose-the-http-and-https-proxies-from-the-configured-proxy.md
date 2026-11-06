---
id: BL-1003
title: Compose the HTTP and HTTPS proxies from the configured proxy listen URLs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1002]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-1003 — Compose the HTTP and HTTPS proxies from the configured proxy listen URLs

## Goal

`Surl.Composition` registers `Surl.Protocol.HttpProxy` for every proxy listen URL in `SurlCommandLine` (from BL-1002),
with BL-551's outbound connector, its proxy accounts, its allowed origins, its limits and, for `https`, its TLS
offering ALPN `http/1.1` only.

## Context

- Second of the three tasks BL-555 was split into (see BL-1002).
- Decision: ADR-0088 decisions 2, 5, 6 and 7; ADR-0088 Amendment 2 (an `IsProxy` `https` listener offers
  `http/1.1` only; without it curl's `--proxy-http2` agrees `h2` and the proxy closes the connection).
- Where: ADR-0124 moved the registration list (`ProtocolServerRegistrations.Original`, `ComposedProtocolServices`)
  into `Application/Surl.Composition.UnitLibrary`; registering the proxy and carrying the outbound `IOriginConnector`
  on `ComposedProtocolServices` are changes there (BL-560's SOCKS composition reuses the connector).
  `ServerTlsComposition` and `AuthenticationComposition` are the TLS and account seams.
- Stand-in: the proxy keeps no store; its exchanges are the engine's events.

## Acceptance criteria

- [ ] Named `Surl.Composition.UnitTests` tests compose an `http` proxy and an `https` proxy (with `IsProxy` and ALPN
      `http/1.1`), forward one request and tunnel one `CONNECT` to a fake origin through the composed connector.
- [ ] Named tests show proxy accounts and allowed origins reaching the server, and each refusal ADR-0088 names at
      start returning its `SurlExitCode`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
