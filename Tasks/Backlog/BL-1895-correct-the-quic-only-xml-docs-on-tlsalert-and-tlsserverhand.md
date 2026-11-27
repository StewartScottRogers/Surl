---
id: BL-1895
title: Correct the QUIC only XML docs on TlsAlert and TlsServerHandshake
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Cryptography/Surl.Tls13.UnitLibrary/TlsAlert.cs, Cryptography/Surl.Tls13.UnitLibrary/TlsServerHandshake.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1895 — Correct the QUIC only XML docs on TlsAlert and TlsServerHandshake

## Goal

The XML docs on `TlsAlert` and `TlsServerHandshake` are true for both QUIC and TLS 1.3 over TCP.

## Context

Found by BL-1878 on 2026-11-27. XML doc comments only. No store, event or fault changes. `Surl.Tls13Stream` also
drives this handshake, for TLS 1.3 over TCP (ADR-0157). In `Cryptography/Surl.Tls13.UnitLibrary/`:

- `TlsAlert.cs` says Surl's TLS 1.3 sends no alert on the wire because QUIC has no record layer. The TCP path
  does send alerts.
- `TlsServerHandshake.cs` describes the class as the handshake for QUIC.
- Its remarks and the `random` parameter doc name only the server random and an x25519 private key as draws;
  the library's `CLAUDE.md` lists more (ML-KEM's m and the other key-share groups' values).

## Acceptance criteria

- [ ] `TlsAlert`'s summary says QUIC reports an alert as a connection error and `Surl.Tls13Stream` sends it over TCP.
- [ ] `TlsServerHandshake`'s summary names both QUIC and TLS 1.3 over TCP.
- [ ] Its remarks and `random` parameter doc match the generator draws the code makes.
- [ ] The diff to the two `.cs` files changes comment lines only, and `dotnet build` has no warnings.

## Notes

## Log

- 2026-11-27: Created.
