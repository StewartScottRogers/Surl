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
completed: 2026-11-04
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

- [x] Both projects exist and are listed in `Surl.slnx` in ordinal order. The production csproj has no
      `PackageReference`. `ProtocolIsolationTests` knows the row and passes.
- [x] RFC 8448 section 3's ClientHello parses into its fields and extensions, and its ServerHello,
      EncryptedExtensions, Certificate, CertificateVerify and Finished are written byte for byte from
      typed values.
- [x] The key schedule fed RFC 8448 section 3's shared secret and transcript produces its early,
      handshake and master secrets, its client and server handshake and application traffic
      secrets, and its Finished keys.
- [x] BL-363's recorded ClientHello from each pinned build parses, and its ALPN list contains `h3`.
- [x] Malformed input (lengths that overrun, a duplicate extension, a legacy version that is not
      `0x0303`, an empty cipher-suite list) fails with the TLS alert RFC 8446 section 6 names, each in
      a named test.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line
      and branch coverage.

## Notes

- Name and place: ADR-0082 decision 2 keeps the planning name, `Surl.Tls13.UnitLibrary`, in
  `Cryptography/`. Its `ProtocolIsolationTests` row is `[Curve25519, MlKem]` as the ADR allows;
  it references neither yet (the key exchange is BL-376's, ML-KEM BL-544's).
- Shape: `ClientHello.Read` (decodes `server_name`, `supported_versions`, `supported_groups`,
  `signature_algorithms`, ALPN, `psk_key_exchange_modes`, `key_share`, and keeps
  `quic_transport_parameters` opaque); writers `ServerHello` (+ `HelloRetryRequest`),
  `EncryptedExtensions`, `CertificateRequest`, `CertificateMessage`, `CertificateVerify`,
  `Finished`, with `TlsExtensions` building the server's extensions; `TlsKeySchedule`
  (RFC 8446 section 7.1, no PSK; `ExpandLabel` public for Surl.Quic's "quic key" labels) and
  `TlsTranscript` (with the section 4.4.1 `message_hash`). Malformed input throws
  `TlsAlertException` carrying the alert; nothing is sent (ADR-0082: no alerts on the wire).
- Alerts chosen from RFC 8446 section 6: overrun lengths, vectors below their minimum (an empty
  cipher-suite list is below `cipher_suites<2..2^16-2>`) and trailing bytes are `decode_error`; a
  repeated extension, a group with two key shares, non-null compression and `pre_shared_key` not
  last are `illegal_parameter` (sections 4.1.2, 4.2.8, 4.2.11); any `legacy_version` other than
  `0x0303` is `protocol_version` (QUIC is TLS 1.3 only, and every TLS 1.3 ClientHello sends
  `0x0303`); a message of the wrong type is `unexpected_message`. Also in the library's CLAUDE.md.
- Tests: RFC 8448 section 3's ClientHello parses field by field; its ServerHello,
  EncryptedExtensions, Certificate, CertificateVerify and Finished are written byte for byte, and
  section 5's HelloRetryRequest too. The key schedule reproduces the early, handshake and master
  secrets, all four traffic secrets, both Finished keys and both verify_data values, and
  `TlsTranscript` reproduces section 5's post-HelloRetryRequest transcript hash. Vectors were
  copied mechanically from rfc8448.txt into `Rfc8448.cs`.
- Fixtures (embedded resources, so the tests stay off the file system): BL-363 left no
  clienthello.bin in the repository, so they were recorded again on 2026-11-04:
  `static-8.21.0-win.bin` (S, `Record-CurlExchange.ps1 -Port 18443 -QuicInitial -Curl
  C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe -CurlArgs
  -sS,-k,--http3-only,--connect-timeout,3,https://localhost:18443/`, 1519 bytes, SNI localhost);
  `curlse-8.22.0_2-win.bin` (W, the same on port 18445, 1479 bytes); `static-8.21.0-linux.bin`
  (L, SHA-256 checked as `153CA463...`, run in WSL against `socat -u UDP4-RECV:18447`, the four
  1200-byte datagrams framed into datagrams.bin and decrypted by `Run-QuicInitialRecorder.cs
  --datagrams`, as ADR-0082 did; 1501 bytes, no SNI for 127.0.0.1). All three offer ALPN `h3`,
  `h3-29`, `supported_versions` 0x0304 only, and an x25519 share; each lists
  TLS_AES_256_GCM_SHA384 first. The osx-arm64 build (M) is not on this machine; it is the same
  source and libraries as S and L, and BL-385 records it in CI's macOS leg.
- Measured: 63 tests; line and branch coverage of the library 100%; `dotnet build -warnaserror`
  clean; fast tests green.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Tls13 reads ClientHello, writes the server's TLS 1.3 handshake messages and computes the RFC 8446 key schedule, all matching RFC 8448 and three recorded upstream curl ClientHellos
