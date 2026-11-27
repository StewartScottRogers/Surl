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
completed: 2026-11-27
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

- [x] The "Malformed input" paragraph states what happens to the alert over QUIC and over TCP.
- [x] The `TlsServerHandshakeOptions` bullet and the ALPN alert row say what TCP listeners pass, as well as QUIC.
- [x] The test-source sentence names every source of expected values in `Surl.Tls13.UnitTests`.
- [x] The only file changed outside `Tasks/` is that `CLAUDE.md`.

## Notes

- `align-and-document` made the edit and read every test file for sources of expected values; the sentence became a
  list. It also corrected the `Receive` bullet, which described the `ClientHello` property and byte levels as QUIC only.
- Prose only. `dotnet build`: 0 warnings, 0 errors. Fast tests: every test project passed.
- Follow-ups: BL-1906 (the hand-built handshake refuses a TCP ClientHello with no ALPN when the listener offers a
  list, against ADR-0010; found by reading code, not measured) and BL-1907 (XML and test comments that read as
  QUIC only). BL-1895, already filed, covers `TlsAlert.cs` and `TlsServerHandshake.cs`.
- The `CLAUDE.md` describes today's ALPN refusal over TCP, so BL-1906 has to update that table row.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Surl.Tls13 CLAUDE.md says which statements hold for QUIC only and which also for TLS 1.3 over TCP, and lists every source of expected test values
