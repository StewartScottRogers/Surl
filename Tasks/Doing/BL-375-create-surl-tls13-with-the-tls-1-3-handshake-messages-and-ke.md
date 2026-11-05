---
id: BL-375
title: Create Surl.Tls13 with the TLS 1.3 handshake messages and key schedule
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-372]
touches: [Surl.slnx, Surl.Tls13.UnitLibrary, Surl.Tls13.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-375 — Create Surl.Tls13 with the TLS 1.3 handshake messages and key schedule

## Goal

`Surl.Tls13.UnitLibrary` and `Surl.Tls13.UnitTests` exist and are listed in `Surl.slnx`. The library reads
and writes the TLS 1.3 handshake messages a QUIC server exchanges, with the extensions BL-372's ADR
supports, and computes the TLS 1.3 key schedule.

## Context

- Decision: BL-372's ADR, decisions 2 and 3. `Surl.Tls13` is the planning name. If the ADR names it
  differently, use its name. If the ADR decides on `System.Net.Quic` everywhere, move this task to
  `Deferred` with the ADR as the reason.
- To create the project, copy BL-292 (commit `5008a375`): the csproj pair, `CLAUDE.md`, ordinal
  `Surl.slnx` lines, and the `ProtocolIsolationTests` row.
- RFC 8446 sections 4.1 to 4.4 cover the messages: ClientHello, ServerHello, HelloRetryRequest,
  EncryptedExtensions, CertificateRequest, Certificate, CertificateVerify and Finished. Section 4.2
  covers the extensions (`supported_versions`, `key_share`, `signature_algorithms`,
  `supported_groups`, `server_name`, `psk_key_exchange_modes`) and section 7.1 the key schedule.
  RFC 7301 covers ALPN, and RFC 9001 section 8.2 covers `quic_transport_parameters`, carried as
  opaque bytes here and parsed by `Surl.Quic`. The library has no record layer: QUIC carries the
  messages in `CRYPTO` frames.
- RFC 8448 section 3 ("Simple 1-RTT Handshake") is a full worked trace: every message and every
  secret.
- BCL: `HKDF`, `SHA256` and `SHA384`. Fixture: BL-363's recorded ClientHello from each pinned
  `HTTP3` build.

## Acceptance criteria

- [ ] Both projects exist and are listed in `Surl.slnx` in ordinal order. The production csproj has no
      `PackageReference`. `ProtocolIsolationTests` knows the row and passes.
- [ ] RFC 8448 section 3's ClientHello parses into its fields and extensions, and its ServerHello,
      EncryptedExtensions, Certificate, CertificateVerify and Finished are written byte for byte from
      typed values.
- [ ] The key schedule fed RFC 8448 section 3's shared secret and transcript produces its early,
      handshake and master secrets, its client and server handshake and application traffic
      secrets, and its Finished keys.
- [ ] BL-363's recorded ClientHello from each pinned build parses, and its ALPN list contains `h3`.
- [ ] Malformed input (lengths that overrun, a duplicate extension, a legacy version that is not
      `0x0303`, an empty cipher-suite list) fails with the TLS alert RFC 8446 section 6 names, each in
      a named test.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
