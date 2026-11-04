---
id: BL-372
title: Decide how Surl serves HTTP/3 over QUIC and what is hand-built
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-363, BL-364]
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-372 — Decide how Surl serves HTTP/3 over QUIC and what is hand-built

## Goal

An ADR decides how Surl serves HTTP/3 (RFC 9114) over QUIC (RFC 9000, 9001, 9002) to pinned upstream
curl, and which parts are hand-built, in which libraries. Its decisions rest on what pinned upstream
curl was measured to send. The QUIC, TLS 1.3, QPACK and HTTP/3 tasks (BL-373 to BL-385) build what it
decides.

## Context

- Scope: the product overview, "Also in scope" ("HTTP versions: 1.0, 1.1, 2 and 3 over QUIC"), and
  Phasing row 6. Root `CLAUDE.md`, "Decisions": nothing is left out because the BCL has no primitive
  for it, and QUIC and HTTP/3 are named among the pieces built by hand, each in its own
  `Surl.<Area>.UnitLibrary` held to the same quality gates. ADR-0010 section 4: "`h3` is QUIC's and
  never travels over `SslStream`".
- The BCL's `System.Net.Quic` depends on the native MsQuic library and is not available on every
  platform. State from Microsoft's .NET 10 documentation, with the URL and the date checked, where
  `QuicListener.IsSupported` is true, and what it needs installed on Windows, Linux and macOS. Weigh
  that against NFR-006 (one native file, nothing else installed) and Rule 2 of the overview's
  architecture (protocol servers receive their transport from the seam). QUIC carries TLS 1.3
  handshake messages in `CRYPTO` frames rather than TLS records (RFC 9001 section 4), which
  `SslStream` cannot produce, so a hand-built QUIC also needs a hand-built TLS 1.3 handshake.
- Measure first, with BL-363's `-QuicInitial`: the ClientHello's cipher suites, groups, signature
  algorithms, ALPN, and the `quic_transport_parameters` that each pinned build with `HTTP3` sends. Those
  builds are the `linux-x64` and `osx-arm64` reference builds and the Windows build that BL-364
  decision 1 chose. Linux and macOS measurements can come from a CI run, as BL-352 did.
- What upstream curl 8.21.0 does (tag `curl-8_21_0`, `docs/cmdline-opts/http3.md` and
  `http3-only.md`): `--http3` tries HTTP/3 and falls back to HTTP/1 or HTTP/2 over TCP after a short
  delay, `--http3-only` has no fallback, and HTTP/3 is available only for HTTPS. Alt-Svc is the other
  way curl reaches HTTP/3.
- Existing pieces:
  - The UDP seam: `IDatagramListener`, `IDatagramFlow` (one flow per remote endpoint) and
    `IDatagramProtocolServer` in `Surl.Protocol.Abstractions` (ADR-0004), implemented in
    `Surl.Networking` by `UdpDatagramListener` and `DatagramDemultiplexer`, used today by TFTP.
  - Hand-built primitives: `Surl.Cryptography.ChaCha20`, `Surl.Cryptography.Poly1305` and
    `Surl.Cryptography.Curve25519` (ADR-0048).
  - HPACK's Huffman code (BL-365).
  - The version-independent answering code (BL-366).
- Limits: ADR-0006 section 1 (`--max-message` covers the HTTP/3 header block) and section 5.

Decide and record:

1. `System.Net.Quic` or a hand-built QUIC, per platform, and why.
2. The libraries and their names, references and rows in ADR-0002's table. Planning assumptions:
   - `Surl.Quic`: packets, frames, packet protection, the connection, streams and recovery.
   - `Surl.Tls13`: the TLS 1.3 server handshake and key schedule, with no record layer.
   - `Surl.Qpack`: references `Surl.Hpack` for the Huffman code.
   - The HTTP/3 frames and server live in `Surl.Protocol.Http`.
3. Which TLS 1.3 cipher suites, key-exchange groups and signature algorithms the server supports, from
   what curl offers. Also whether the BCL's `AesGcm` and `ChaCha20Poly1305` are used where
   `IsSupported`, or the hand-built ChaCha20 and Poly1305.
4. Which QUIC features are built: Retry, version negotiation, 0-RTT, connection migration, key
   update, the connection IDs issued, the transport parameters sent, and the loss detection and
   congestion controller (RFC 9002).
5. The seam: whether QUIC uses `IDatagramFlow` per remote endpoint or a new contract in
   `Surl.Protocol.Abstractions` (BL-380), and what `Surl.Networking` adds (BL-381).
6. The command line: whether `surl https://host:port` also answers HTTP/3 on UDP at the same port by
   default, any option to turn it on or off, and whether responses carry `Alt-Svc`.
7. How each ADR-0006 limit maps onto QUIC and HTTP/3 (streams, flow-control credit, header block,
   idle and head timeouts, handshake time), and the error each ends with.
8. QPACK: whether the dynamic table is offered (`SETTINGS_QPACK_MAX_TABLE_CAPACITY`) and blocked
   streams.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked
      "Decided by Claude under Stewart's delegation". It answers decisions 1 to 8, with the measured
      ClientHello table of each pinned `HTTP3` build, and is listed in that folder's `README.md`.
- [ ] ADR-0002's table and ADR-0010 section 4 are amended, or point to the new ADR, so that neither
      states something the new ADR changes.
- [ ] `Documentation/Product/Requirements.md` has a functional requirement row for HTTP/3, in the
      shape of FR-048.
- [ ] `Documentation/Wiki/Glossary.md` defines the terms the ADR introduces (for example QUIC, QPACK,
      connection ID, packet number space, encryption level).

## Notes

## Log

- 2026-11-04: Created.
