---
id: BL-370
title: Offer h2 by ALPN on https and serve HTTP/2 over TLS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-368]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests, Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Surl.Conformance.UnitTests, Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-370 — Offer h2 by ALPN on https and serve HTTP/2 over TLS

## Goal

`surl https://` offers the ALPN list that BL-364's ADR decides, `h2` included, and serves HTTP/2 on
every TLS connection where `h2` was agreed. A connection that agreed `http/1.1`, or no ALPN at all,
is served as it is today.

## Context

- Decision: BL-364's ADR, decision 2. That decision covers the list, the option that turns HTTP/2
  off (if there is one), and what happens to `wss`.
- `Surl.Networking.UnitLibrary/TlsApplicationProtocols.cs` offers `http/1.1` for `https` and `wss`.
  `TlsApplicationProtocolsTests` pins that. ADR-0010 section 4 is amended by BL-364.
- The HTTP server reads the agreed protocol from `TlsSession.ApplicationProtocol`
  (`Surl.Protocol.Abstractions.UnitLibrary/TlsSession.cs`, ADR-0010) and picks HTTP/2 (BL-368's
  connection) or HTTP/1.x.
- Once `h2` is offered, the Linux and macOS reference builds negotiate HTTP/2 for every `https` test
  in `Surl.Conformance.UnitTests` (curl negotiates HTTP/2 for HTTPS by default:
  `docs/cmdline-opts/http2.md`, tag `curl-8_21_0`). Adapt the tests that pin HTTP/1.1 output, as the
  ADR says, in this same diff. Otherwise CI's Linux and macOS legs go red. Lanes test only on Windows,
  where the reference build has no `HTTP2`, so read every `https` conformance test for this.
- If a new option is added, it needs its `--aihelp` facts and topic pages (root `CLAUDE.md`).

## Acceptance criteria

- [ ] `TlsApplicationProtocolsTests` pins the ADR's list for `https` and `wss`, and the list for every
      other scheme is unchanged.
- [ ] An `InMemoryConnection` test with `ApplicationProtocol` `h2` is served as HTTP/2, and tests with
      `http/1.1` and with `null` are served as HTTP/1.1 with unchanged bytes.
- [ ] Every `https` test in `Surl.Conformance.UnitTests` that pins HTTP/1.1 output either pins the
      HTTP version it asks for (for example `--http1.1`) or pins the HTTP/2 output, as the ADR
      decides. Notes list each test changed.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, both libraries keep 100% line
      and branch coverage, and `dotnet test Surl.Conformance.UnitTests --filter "FullyQualifiedName~OverHttps"`
      passes on Windows.

## Notes

## Log

- 2026-11-04: Created.
