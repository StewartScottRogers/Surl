---
id: BL-1894
title: Correct the QUIC only statements in the Tls13 library CLAUDE.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Cryptography/Surl.Tls13.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1894 — Correct the QUIC only statements in the Tls13 library CLAUDE.md

## Goal

`Surl.Tls13.UnitLibrary`'s `CLAUDE.md` says which of its statements hold for QUIC only and which also hold for TLS 1.3 over TCP.

## Context

Found by BL-1878 on 2026-11-27. Prose only. No store, event or fault changes. `Surl.Tls13Stream` also drives this
handshake, for TLS 1.3 over TCP (ADR-0157). In `Cryptography/Surl.Tls13.UnitLibrary/CLAUDE.md`:

- "Malformed input" says nothing is sent on the wire and the caller reports a QUIC error. Over TCP,
  `Surl.Tls13Stream` sends the fatal alert.
- The `TlsServerHandshakeOptions` bullet says only the RFC 8448 test (not QUIC) turns off the ALPN and
  transport-parameter checks. `ServerTlsSettings.CreateTls13Options` leaves the transport parameters null for TCP
  and passes the listener's ALPN list, which may be empty.
- The `h3` ALPN list and the "no `h3` (or no ALPN at all)" alert row describe QUIC only.
- "Every expected value in its tests comes from RFC 8448 ... or from ClientHellos" looks too narrow:
  `UnitTestFiniteFieldPrimes.cs` copies the RFC 7919 primes, and `TlsRecordProtectionTests.cs` derives a value
  from RFC 8446 section 7.2.

## Acceptance criteria

- [ ] The "Malformed input" paragraph states what happens to the alert over QUIC and over TCP.
- [ ] The `TlsServerHandshakeOptions` bullet and the ALPN alert row say what TCP listeners pass, as well as QUIC.
- [ ] The test-source sentence names every source of expected values in `Surl.Tls13.UnitTests`.
- [ ] The only file changed outside `Tasks/` is that `CLAUDE.md`.

## Notes

## Log

- 2026-11-27: Created.
