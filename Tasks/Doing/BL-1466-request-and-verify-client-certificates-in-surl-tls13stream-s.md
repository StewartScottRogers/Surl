---
id: BL-1466
title: Request and verify client certificates in Surl.Tls13Stream so --tlsv1.3 --cacert is served on macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1454]
touches: [Cryptography/Surl.Tls13Stream.UnitLibrary, Cryptography/Surl.Tls13Stream.UnitTests, Cryptography/Surl.Tls13.UnitLibrary, Cryptography/Surl.Tls13.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1466 — Request and verify client certificates in Surl.Tls13Stream so --tlsv1.3 --cacert is served on macOS

## Goal

`Surl.Tls13Stream` requests a client certificate and verifies it against the listener's `--cacert` roots, so a listener limited to TLS 1.3 with `--cacert` serves on macOS as it does on Linux.

## Context

ADR-0157 amendment 1 (BL-1453) decides this. ADR-0157 decision 3 left client certificates to `SslStream`; macOS `SslStream` has no TLS 1.3 server, so `--tlsv1.3 --cacert` ends with curl exit 35 there. Needs CertificateRequest, client Certificate and CertificateVerify handling (RFC 8446 4.3.2, 4.4.2, 4.4.3) in `Surl.Tls13`'s handshake and the stream, and the settings' chain verification (ADR-0010 section 5). Routing is BL-1454.

## Acceptance criteria

- [ ] The Tls13 handshake sends CertificateRequest when the settings ask for a client certificate and verifies the client's Certificate and CertificateVerify, with unit tests for accept, wrong signature, untrusted chain and no certificate.
- [ ] `Tls13ServerStream` exposes the verified client certificate in the session the way `SslStream` does.
- [ ] Unit tests over the in-memory duplex pair pass; both libraries keep 100% line and branch coverage.
- [ ] Notes name the follow-up that lifts the routing guard for client-certificate settings, if one is still needed.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
