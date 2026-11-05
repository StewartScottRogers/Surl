---
id: BL-377
title: Complete the QUIC handshake on a Surl.Quic server connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-374, BL-376]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-377 — Complete the QUIC handshake on a Surl.Quic server connection

## Goal

A `Surl.Quic` server connection completes the QUIC handshake with a client over a datagram exchange.
It processes Initial and Handshake packets, carries BL-376's TLS 1.3 handshake in `CRYPTO` frames,
exchanges transport parameters, sends `HANDSHAKE_DONE`, and then opens and seals 1-RTT packets. It
acknowledges every packet number space, and ends with `CONNECTION_CLOSE` on error, idle timeout or
shutdown.

## Context

- Decision: BL-372's ADR, decisions 4 (connection IDs, Retry, version negotiation, transport
  parameters) and 7 (limits and timeouts).
- RFC 9000 sections 5 (connection IDs), 7 (handshake), 8.1 (address validation and the anti-amplification
  limit of three times the bytes received), 10 (closing, idle timeout), 13.2 (generating ACKs), 14
  (datagram size, the 1,200-byte minimum for client Initials), and 18 (transport parameters). RFC
  9001 section 4 (encryption levels, key discard).
- Builds on BL-373's codec, BL-374's protection and BL-376's handshake. Time comes from an injected
  `TimeProvider`. The connection talks to a datagram abstraction that tests fake, and never to a
  socket (the seam is BL-380).
- Fixtures: BL-363's recorded client Initial datagrams from each pinned build.

## Acceptance criteria

- [x] With BL-363's recorded client Initial from each pinned build as input, the connection answers
      with Initial and Handshake packets that a test-side QUIC client in the test project decrypts,
      and completes the handshake with that client.
- [x] Named tests cover: a client Initial smaller than 1,200 bytes dropped; a server that sends no
      more than three times the bytes it received before address validation; an unsupported version
      answered with Version Negotiation; the idle timeout closing silently, measured with a fake
      `TimeProvider`; a protocol violation ending with `CONNECTION_CLOSE` and the RFC 9000 error code;
      and Initial and Handshake keys discarded when RFC 9001 section 4.9 says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- Shape: `QuicServerConnection` (public, partial: `.Receiving`, `.Sending`) takes datagrams and
  returns `QuicConnectionOutput` (datagrams, and the 1-RTT frames that are the caller's), with
  `HandleTimeout`/`NextDeadline`, `SendApplicationFrames` and `Close(applicationErrorCode)`. It does
  no I/O, as ADR-0082 decision 2 says; "the datagram abstraction that tests fake" is therefore the
  byte-array in/out API itself, and the loop over an `IDatagramFlow` is BL-572's (ADR-0086).
  `QuicServerConnectionOptions` carries the certificate, ALPN (`h3` by default), the idle and
  handshake timeouts and the credit decision 4 grants, so ADR-0086's other callers can change them.
  New: `QuicTransportParameters` and `QuicVersionInformation` (public), `QuicPacketNumberSpace`,
  `QuicReceivedPacketNumbers`, `QuicCryptoReassembly` (internal), and
  `QuicTransportErrorCode.VersionNegotiationError` (0x11, RFC 9368).
- Choices within ADR-0082 and the RFCs, taken as sensible defaults (no new ADR; each applies a rule
  the ADR or an RFC states):
  - A first datagram starts the connection only when it is a version 1 Initial of at least 1200
    bytes with a Destination Connection ID of at least 8 bytes (RFC 9000 sections 7.2 and 14.1); a
    long header of another version and at least 1200 bytes gets Version Negotiation (unused bits
    0x40, as RFC 9000 section 17.2.1 recommends); anything else is dropped.
  - Every ack-eliciting packet is acknowledged in the same call, ACK Delay 0 (the server never
    delays, so 0 is its true delay). At most 32 ACK ranges are kept; older numbers count as seen.
  - CRYPTO data more than 64 KiB past what has been delivered is `CRYPTO_BUFFER_EXCEEDED` (RFC 9000
    section 7.5 asks for at least 4096; the largest ClientHello measured is 1519 bytes).
  - Before the handshake completes, `CONNECTION_CLOSE` goes at Initial and at Handshake, whichever
    the server holds keys for, since it cannot know which the client opens (RFC 9000 section
    10.2.3; ADR-0082 decision 7's "highest level both sides have" is covered by sending both). After
    it, in 1-RTT only. No reason phrase is sent (ADR-0006: nothing internal on the wire). An
    application close before the handshake is `APPLICATION_ERROR` in a 0x1c frame (section 10.2.3).
  - An ACK of a packet number never sent is `PROTOCOL_VIOLATION` (section 13.1 allows it).
    `NEW_TOKEN`, `HANDSHAKE_DONE` and `RETIRE_CONNECTION_ID` from the client are
    `PROTOCOL_VIOLATION` (sections 19.7, 19.20, 19.16: the server's only connection ID is in use).
    `PATH_CHALLENGE` gets `PATH_RESPONSE`; `NEW_CONNECTION_ID` and `PATH_RESPONSE` are taken and
    not acted on (BL-607).
  - The client's transport parameters: a server-only one, or an `initial_source_connection_id`
    absent or not the client's Source Connection ID, is `TRANSPORT_PARAMETER_ERROR`;
    `version_information` choosing a version other than 1 is `VERSION_NEGOTIATION_ERROR`. The idle
    timeout is the smaller of the two sides' (0 means none; a client's is capped at 2^31-1 ms).
  - 1-RTT packets that arrive before the handshake completes are dropped, not buffered (RFC 9001
    section 5.7 allows either); the client retransmits.
  - Packet numbers are as short as RFC 9000 Appendix A.2 allows given what the client acknowledged;
    a payload too short to sample is padded with PADDING.
- Left out, filed: key updates (BL-606, ADR-0082 decision 4 says the server follows them; today a
  packet in the other key phase does not open and is dropped), the client's spare connection IDs
  and `retire_prior_to` (BL-607), and the closing and draining periods (BL-608, needs BL-379's PTO).
  Nothing is retransmitted until BL-379.
- Fixtures: BL-363 left no datagrams in the repository, so each pinned build was recorded again on
  2026-11-04 into `Surl.Quic.UnitTests/Fixtures/ClientInitial/*.datagrams.bin` (two-byte length
  before each datagram): `static-8.21.0-win` (`Record-CurlExchange.ps1 -Port 18461 -QuicInitial
  -Curl C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe -CurlArgs
  -sS,-k,--http3-only,--connect-timeout,3,https://localhost:18461/`, exit 28, SHA-256 of the file
  6862A145...), `curlse-8.22.0_2-win` (the same with the curl.se build on port 18463, 6FB6BDFB...),
  and `static-8.21.0-linux` (WSL Ubuntu, `/opt/upstream-curl/8.21.0/curl` checked as 153CA463...,
  `--http3-only --connect-timeout 3 https://127.0.0.1:18465/` captured by `socat -u UDP4-RECV:18465`,
  4 x 1200 bytes framed with PowerShell and checked with `Run-QuicInitialRecorder.cs --datagrams`,
  C4BF1E89...). Each is four 1200-byte Initials, packet numbers 0 to 3, CRYPTO frames shuffled and
  repeated, as ADR-0082 measured.
- Test client: `UnitTestQuicClient` opens each recorded Initial with the client Initial keys, writes
  its own x25519 public key over curl's in the CRYPTO frames' bytes (and, for fault tests, a
  same-length edit of the ClientHello), and seals it again with the same header fields and packet
  number. Curl writes the Length field in four bytes and Surl.Quic in two, so the final PADDING run
  grows by two bytes and each datagram stays 1200 bytes; every frame is curl's. Its TLS half,
  `UnitTestTls13Client`, is written from RFC 8446 like Surl.Tls13.UnitTests' client and shares no
  code with Surl.Tls13; packet protection is Surl.Quic's own `QuicPacketProtection` (checked against
  RFC 9001 Appendix A in BL-374). All three builds complete: TLS_AES_256_GCM_SHA384, x25519, ALPN
  `h3`, `HANDSHAKE_DONE` in 1-RTT, then 1-RTT frames both ways.
- Measured: 297 tests in Surl.Quic.UnitTests (107 new: 74 for the connection, 3 for the reassembly, 4 for
  the packet numbers, 26 for the transport parameters); `Measure-CodeQuality.ps1 -Library
  Surl.Quic.UnitLibrary`: line 100%, branch 100%, 0 failing members, worst CRAP 10. `dotnet build
  -warnaserror` clean; `dotnet format --verify-no-changes` clean for both projects; fast tests green
  in all 44 test assemblies. Two defects the tests caught on the way: absent `disable_active_migration`
  and `version_information` were written as empty parameters (a nullable conversion), and the
  forgotten-range floor of the ACK tracker was one too low.
- Not run: a live upstream curl against the connection. That needs the HTTP/3 server and UDP
  listener (BL-381, BL-385); the recorded datagrams are curl's own bytes in the meantime.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. A Surl.Quic server connection completes the QUIC handshake with each pinned curl build's recorded Initials: CRYPTO-carried TLS 1.3, transport parameters, ACKs, anti-amplification, key discard, HANDSHAKE_DONE, 1-RTT both ways, CONNECTION_CLOSE on error
