---
id: BL-475
title: Serve CoAP over DTLS 1.2 for coaps
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-474, BL-415, BL-650]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Documentation/Planning/Decisions/ADR-0097-how-the-coap-server-answers-coap-and-coaps.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-475 — Serve CoAP over DTLS 1.2 for coaps

## Goal

The CoAP server serves `coaps` over `Surl.Dtls` in the modes BL-403's ADR chose, with PSK identities and
certificates from its caller. Every exchange, block-wise transfer and observation works as over `coap`, and a
peer that has not finished its handshake gets nothing.

## Context

- Decisions: BL-470's ADR, decision 6, and BL-403's ADR.
- `Surl.Dtls` (BL-412 to BL-415; add its reference, which its ADR-0002 row allows). RFC 7252 section
  9.1: one DTLS session per client endpoint, and CoAP messages as DTLS application data. Section 9.1.1: matching
  responses to requests within the same session, epoch included.
- PSK identity lookup is wired to the accounts through the `Surl.Protocol.Abstractions` contracts.

## Acceptance criteria

- [x] Named tests, with a DTLS test client from `Surl.Dtls.UnitTests`' fixtures or written in this test project from
      RFC 6347, show:
      - a PSK handshake then a `GET` answered;
      - a certificate handshake then a block-wise `GET`;
      - an observation notified over DTLS;
      - a CoAP datagram before the handshake finishes ignored;
      - an unknown PSK identity refused as the DTLS ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Plan, as built: `SecureCoapProtocolServer` (scheme `coaps`, no refusal writer) runs a `DtlsServerSession`
  per flow, then wraps the association as `CoapDtlsFlow` (an `IDatagramFlow` of decrypted records) and serves
  it through `CoapFlowServing`, the code `CoapProtocolServer` now also uses, so every exchange, block-wise
  transfer and observation is the `coap` code path. Credentials come in a `CoapDtlsCredentials` record.
- Tests (`SecureCoapProtocolServerTests`), one per criterion:
  `ServeAsync_PskHandshakeThenGet_IsAnswered205AsAnAuthenticatedPeerAndNotesThePskLogin`,
  `ServeAsync_CertificateHandshakeThenBlockwiseGet_IsAnsweredIn1024ByteBlocks`,
  `ServeAsync_ObservationOverDtls_IsNotifiedOfAPutOverPlainCoap`,
  `ServeAsync_PlaintextCoapBeforeTheHandshakeFinishes_IsIgnored` (a plaintext GET as the flow's first datagram
  and again inside the handshake), `ServeAsync_UnknownPskIdentity_IsRefusedWithDecryptErrorAndServesNothing`;
  plus `max_fragment_length` 512 shrinking blocks to 256, client-certificate and no-certificate access rules,
  head timeout, client `close_notify`, and `close_notify` at a limit but not at shutdown.
- The DTLS test client (`UnitTestDtlsClient`, `UnitTestCertificates`) is copied from `Surl.Dtls.UnitTests` into
  this test project, as the criterion allows; only its initial-sequence-number knob was dropped, because it
  needs a `Surl.Dtls` internal. A project reference between two test projects was the alternative, rejected.
- Context said the PSK lookup goes "through the `Surl.Protocol.Abstractions` contracts"; ADR-0097 decision 6
  says no contract is added and the lookup is `Surl.Dtls`'s caller-given `DtlsPreSharedKeyLookup`, which BL-476
  builds from the accounts. The ADR wins: the server takes the lookup in `CoapDtlsCredentials`.
- Decisions recorded in ADR-0097's BL-475 amendment (cookie secret per server from the first flow's clock,
  limits from `ExchangeLimits`, the 64-byte block header room, how a flow ends, serialised sends). The ADR file
  was added to `touches`: no task in Doing names it.
- Coverage of `Surl.Extended.Protocol.Coap.UnitLibrary`: 100% line, 100% branch (290 tests).
- Also: `dotnet format` fixed one missing space in `CoapFlowObservers.IsObservable`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. CoAP serves coaps over Surl.Dtls (PSK, certificate, client certificate), with block-wise transfers and observations on the coap code path
