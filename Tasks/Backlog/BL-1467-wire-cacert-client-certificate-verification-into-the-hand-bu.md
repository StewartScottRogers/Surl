---
id: BL-1467
title: Wire --cacert client-certificate verification into the hand-built TLS 1.3 server stream in Surl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1466]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1467 — Wire --cacert client-certificate verification into the hand-built TLS 1.3 server stream in Surl.Networking

## Goal

A listener limited to TLS 1.3 with `--cacert` requests and verifies the client certificate through `Tls13ServerStream`, so it is served on macOS as on Linux.

## Context

ADR-0157 amendment 1. BL-1466 made `TlsServerHandshakeOptions.TrustsClientCertificateChain` and `Tls13ServerStream.ClientCertificateChain` work over the stream. `ServerTlsSettings.CreateTls13Options` still leaves the delegate null, so the hand-built path requests no certificate, and `ServerTlsHandshake.SessionOf(Tls13ServerStream)` passes a null client certificate. Start there; `ClientCertificateVerifier.IsTrusted` takes the leaf only, so decide how the intermediates in the chain reach it (ADR-0010 section 5).

## Acceptance criteria

- [ ] `CreateTls13Options` sets `TrustsClientCertificateChain` from the client-certificate verifier when `--cacert` is given, and leaves it null otherwise, with unit tests for both.
- [ ] The TLS session of a hand-built stream carries the verified client certificate, with a unit test.
- [ ] A TLS 1.3-only listener with `--cacert` refuses a client without a trusted certificate with a handshake failure, in a test over the hand-built stream.
- [ ] No guard sends `--tlsv1.3 --cacert` listeners to `SslStream` any more; both projects keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-20: Created.
