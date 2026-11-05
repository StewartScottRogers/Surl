---
id: BL-413
title: Read and write DTLS 1.2 handshake messages with fragmentation and the TLS 1.2 PRF
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-412]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-413 — Read and write DTLS 1.2 handshake messages with fragmentation and the TLS 1.2 PRF

## Goal

`Surl.Dtls` encodes and decodes every DTLS 1.2 handshake message a server sends or reads. It fragments outgoing
messages to the path MTU, reassembles incoming fragments in any order, and computes the TLS 1.2 PRF, master
secret and key block.

## Context

- Decision: BL-403's ADR (the extensions answered, the largest message, the MTU).
- RFC 6347 section 4.2.2 (the 12-byte handshake header with `message_seq`, `fragment_offset` and
  `fragment_length`), section 4.2.3 (fragmentation and reassembly), and section 4.2.1 (`HelloVerifyRequest`).
- RFC 5246 section 7.4 (ClientHello, ServerHello, Certificate, ServerKeyExchange, CertificateRequest,
  ServerHelloDone, ClientKeyExchange, CertificateVerify, Finished) and section 5 (the PRF with HMAC-SHA256).
- RFC 4492 / RFC 8422 (ECDHE parameters), RFC 4279 (PSK identity hint and identity), RFC 7627 (extended master
  secret).
- Test vectors: the PRF is checked against a published TLS 1.2 PRF vector. Cite the source in the test's doc
  comment.

## Acceptance criteria

- [ ] Named tests cover:
      - each message type round-tripped;
      - a ClientHello with a cookie decoded, with its extensions listed;
      - a message split over three fragments and reassembled when they arrive out of order;
      - a duplicate fragment ignored;
      - a fragment past the ADR's largest-message limit refused;
      - the PRF and extended master secret matching the cited vector.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
