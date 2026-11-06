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
completed: 2026-11-05
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

- [x] Named tests cover:
      - each message type round-tripped;
      - a ClientHello with a cookie decoded, with its extensions listed;
      - a message split over three fragments and reassembled when they arrive out of order;
      - a duplicate fragment ignored;
      - a fragment past the ADR's largest-message limit refused;
      - the PRF and extended master secret matching the cited vector.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Built in `Surl.Dtls` (ADR-0087 decisions 5, 7, 9): `DtlsHandshakeHeader`, `DtlsHandshakeMessage`, one record
  per body (`DtlsClientHello` ... `DtlsFinished`, `DtlsExtension`), `DtlsHandshakeFragmenter`,
  `DtlsHandshakeReassembler` with `DtlsHandshakeFragmentOutcome`, and `DtlsPseudorandomFunction`. The library's
  `CLAUDE.md` lists them.
- Tests, by criterion: round trips in `DtlsHandshakeMessagesTests` (every message type, plus both
  `ClientKeyExchange` forms and the RawPublicKey `Certificate`);
  `ClientHello_Read_WithCookie_DecodesEveryFieldAndListsItsExtensions`;
  `Accept_ThreeFragmentsOutOfOrder_ReassemblesTheMessage`; `Accept_DuplicateFragment_IsIgnored`;
  `Accept_MessageLongerThanTheLimit_IsOverLimit`; `Compute_Sha256Vector_MatchesThePublishedOutput`,
  `Compute_Sha384Vector_MatchesThePublishedOutput` and `ExtendedMasterSecret_VectorSecretAndSeed_IsThePrfUnderItsLabel`.
- PRF vector: Joseph Birr-Pixton's TLS 1.2 PRF test vectors on the IETF TLS list (cited in the test's doc
  comment). Both the SHA-256 and SHA-384 outputs were also reproduced by an independent PowerShell P_hash over
  the BCL's HMAC before being pinned. No published extended-master-secret vector exists in that set, so the EMS
  expected bytes come from that same independent PowerShell computation of RFC 7627 section 4's definition.
- Choices taken (defaults, within ADR-0087): the largest-message limit is a constructor argument
  (`--max-message`, decision 9) bounding every body held at once, not one message alone; a hello extension
  type appearing twice is `decode_error` (RFC 5246 section 7.4.1.4 forbids it); a curve type other than
  `named_curve` in a `ServerKeyExchange` is `decode_error`. The reassembler buffers any later `message_seq`
  within the limit; dropping fragments of a later flight than the next (decision 9) needs flight knowledge, so
  it stays with the handshake state machine in BL-414. No PSK `ServerKeyExchange` codec: the server sends no
  identity hint (decision 3).
- One test class covers all message codecs and one covers the body reader and writer, because they share the
  RFC-written fixtures; every other new production class has its own `<Class>Tests`.
- Gates: `dotnet build -warnaserror` clean; fast tests green (Surl.Dtls.UnitTests 118); `Measure-CodeQuality.ps1
  -Library Surl.Dtls.UnitLibrary`: 100% line, 100% branch, 124 members, worst CRAP 10.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Dtls reads and writes every DTLS 1.2 handshake message, fragments to a budget, reassembles out of order, and computes the TLS 1.2 PRF, master secrets, key block and Finished
