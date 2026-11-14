---
id: BL-1147
title: Serve TLS 1.3 through Surl.Tls13Stream where SslStream cannot, so upstream test 4001 gets exit 101 on macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1146]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1147 — Serve TLS 1.3 through Surl.Tls13Stream where SslStream cannot, so upstream test 4001 gets exit 101 on macOS

## Goal

`ServerTlsHandshake` hands a TLS 1.3-only ClientHello to `Tls13ServerStream` when the platform's `SslStream` has no TLS 1.3 server.

## Context

- ADR-0157 decisions 1 and 2 (third bullet). Predicate: injectable `ITls13Support` (macOS by `OperatingSystem.IsMacOS()`) and the ClientHello's `supported_versions` lists 1.3 and nothing lower; no client-certificate setting; otherwise `SslStream` as today.
- Read the ClientHello record first and replay its bytes to whichever stream is chosen; fill `TlsSession` the same way as for `SslStream`.
- The end result (4001 exit 101 on `osx-arm64`) is measured by BL-1142's CI run.

## Acceptance criteria

- [ ] Unit tests with a fake `ITls13Support` show: TLS 1.3-only hello on an unsupported platform gets `Tls13ServerStream`; a hello also offering 1.2, or a supported platform, gets `SslStream`; a client-certificate setting gets `SslStream`.
- [ ] The returned `TlsSession` reports protocol, cipher suite and ALPN for the hand-built stream.
- [ ] `ServerTlsHandshake` still never constructs a `Socket`, `TcpListener`, `UdpClient` or `HttpListener` outside the existing types.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-14: Created.
