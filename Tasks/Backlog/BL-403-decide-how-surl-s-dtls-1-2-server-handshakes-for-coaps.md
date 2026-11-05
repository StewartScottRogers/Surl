---
id: BL-403
title: Decide how Surl's DTLS 1.2 server handshakes for coaps
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-401]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed:
---
# BL-403 — Decide how Surl's DTLS 1.2 server handshakes for coaps

## Goal

An ADR decides what Surl's hand-built DTLS 1.2 server (RFC 6347 over RFC 5246) supports for `coaps`: cipher
suites, credentials, the cookie exchange, retransmission and limits. The DTLS tasks BL-412,
BL-413, BL-414 and BL-415 then build what it decides.

## Context

- BL-401's ADR decides the library's name, folder and ADR-0002 row (planning name `Surl.Dtls`).
- What the specifications require of a CoAP server:
  - RFC 7252 section 9.1: `coaps` runs over DTLS 1.2 on UDP port 5684.
  - Section 9.1.3.1 (PreSharedKey mode) mandates `TLS_PSK_WITH_AES_128_CCM_8` (0xC0A8).
  - Sections 9.1.3.2 and 9.1.3.3 (RawPublicKey and Certificate modes) mandate
    `TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8` (0xC0AE) with secp256r1.
- What the port's client offers. This only tells us what to accept and is not a verdict; it comes from the
  port's `Documentation/Conformance/Extended/Coap-Dtls-Remaining.md`, row `Coap:RFC6347-4.3-1`: the AEAD
  suites 0xC02B, 0xC02F and 0xC0AE. The port's `DtlsReferenceServer` uses 0xC02F with a self-signed RSA
  certificate.
- BCL primitives available: `AesGcm`, `AesCcm` (an 8-byte tag is allowed), `ECDiffieHellman` (P-256),
  `ECDsa`, `RSA`, `HMACSHA256`, `X509Certificate2`. Hand-built: the TLS 1.2 PRF (RFC 5246 section 5), the
  record layer and the handshake. `SslStream` cannot do any of this over datagrams.
- Surl's certificate options already serve `https` (ADR-0010). Decide whether `coaps` takes the same
  `--cert`/`--key`, and how a PSK identity and key are given. Planning assumption: from the accounts file, so
  that secure-by-default holds and no PSK is built in.
- RFC 6347 section 4.2.1: the stateless cookie (HelloVerifyRequest) is a SHOULD and is the server's defence
  against amplification. Section 4.2.4: retransmission timers, driven by an injected `TimeProvider`.
  Section 4.1.2.6: the replay window.
- Limits (ADR-0006): handshake time, the largest fragment and reassembled message, and the number of flows.

Decide and record: the suites offered and their preference order; the certificate, RawPublicKey (RFC 7250)
and PSK modes and how each is configured; the cookie secret and its lifetime; the retransmission timer values;
renegotiation (refused), session resumption, and the extensions answered (`extended_master_secret` RFC 7627,
`renegotiation_info`, `supported_groups`, `ec_point_formats`, `signature_algorithms`); the alert sent for each
failure; and how each ADR-0006 limit applies.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers each point above, citing the RFC section behind each
      choice, and is listed in the folder's `README.md`.
- [ ] `Documentation/Wiki/Glossary.md` is not touched here. The DTLS terms join it with BL-414.

## Notes

## Log

- 2026-11-04: Created.
