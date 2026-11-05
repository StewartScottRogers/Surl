---
id: BL-727
title: Verify client certificates over QUIC so --http3 works with --cacert
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-384]
touches: [Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-727 — Verify client certificates over QUIC so --http3 works with --cacert

## Goal

`surl --http3 --cacert <file> https://...` serves HTTP/3 and requires a client certificate that
chains to the `--cacert` anchors, as the TCP listener does (ADR-0010 section 5).

## Context

- BL-384 refuses `--http3` with `--cacert` (`option --http3: cannot be used with --cacert`, exit
  2), because `QuicServerConnection` builds its `TlsServerHandshakeOptions` without
  `TrustsClientCertificateChain`, so an HTTP/3 connection would skip the client-certificate check
  the TCP side enforces.
- `Surl.Tls13`'s `TlsServerHandshakeOptions.TrustsClientCertificateChain` already exists;
  `QuicServerConnectionOptions` needs to pass it through, and `Http3ApplicationProtocol` should
  put the verified certificate on the `TlsSession` it starts the authentication session with
  (the `external` mechanism reads it).
- Upstream curl's `--cert` over `--http3`: measure with `Record-CurlExchange.ps1` before pinning.

## Acceptance criteria

- [ ] `CommandLineParser` no longer refuses `--http3` with `--cacert`; its test and the
      `--aihelp http` paragraph and manual's HTTP OPTIONS say so.
- [ ] A `Surl.Quic.UnitTests` test shows a handshake without a client certificate failing, and one
      with a chaining certificate succeeding, when the options carry the trust check.
- [ ] `dotnet build -warnaserror` is clean, fast tests green, touched projects at 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
