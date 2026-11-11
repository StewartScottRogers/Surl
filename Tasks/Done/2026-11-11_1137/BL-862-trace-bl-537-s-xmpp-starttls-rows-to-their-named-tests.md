---
id: BL-862
title: Trace BL-537's XMPP STARTTLS rows to their named tests
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-537]
touches: [Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-862 — Trace BL-537's XMPP STARTTLS rows to their named tests

## Goal

Each row of `Documentation/Conformance/Extended/Xmpp.md` that BL-537 delivered names its test instead of `BL-537`,
and the two rows it did not deliver name the task that does.

## Context

- BL-537 built STARTTLS in `Surl.Extended.Protocol.Xmpp.UnitLibrary`; its tests are in
  `Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests/XmppServerStartTlsTests.cs`. It could not edit the matrix
  because `Documentation/Conformance` was in BL-396's `touches` while it ran.
- Rows and their tests:
  - `Xmpp:RFC6120-5.3.1-1`: `ServeAsync_CertificateConfigured_OffersRequiredStartTlsAlone`.
  - `Xmpp:Surl-Tls-1`: `ServeAsync_AllowPlaintextAuthentication_OffersVoluntaryStartTlsBesideThePlainTextMechanisms`.
  - `Xmpp:RFC6120-6.5.4-1`: `ServeAsync_SaslAuthBeforeTlsWhileRequired_IsEncryptionRequiredAndTheStreamKept`.
  - `Xmpp:RFC6120-5.3.1-2`: `ServeAsync_OtherElementBeforeTls_EndsTheStreamAsSection5Says`.
  - `Xmpp:RFC6120-5.4.2.3-1`: `ServeAsync_StartTls_IsAnsweredProceedThenTheUpgrade`.
  - `Xmpp:RFC6120-5.4.2.2-1`: `ServeAsync_StartTlsWithoutACertificate_IsTheTlsFailureAndClose` and
    `ServeAsync_StartTlsRepeatedAfterTls_IsTheTlsFailureAndClose`.
  - `Xmpp:RFC6120-5.4.3.2-1`: `ServeAsync_HandshakeFails_ClosesWithNoMoreBytes`.
  - `Xmpp:RFC6120-5.4.3.3-1`: `ServeAsync_StreamRestartedAfterTls_HasANewIdAndOffersSaslAndNotStartTls`.
  - `Xmpp:XEP0368-1` (the handshake from the first byte) and `Xmpp:Surl-Tls-2` (failing at start without a
    certificate) are the composition's: BL-540 wraps `xmpps` in `ImplicitTlsSchemeServer` and checks certificates
    at start. `ServeAsync_ConnectionTlsFromItsFirstByte_OffersSaslAndRefusesStartTls` covers the server's half of
    `Xmpp:XEP0368-1`.

## Acceptance criteria

- [x] Each row above names the test(s) listed, in the matrix's existing style.
- [x] `Xmpp:XEP0368-1` and `Xmpp:Surl-Tls-2` name BL-540 (the first also the server-side test above).

## Notes

Rows now name BL-537's tests (backticked, comma-separated); matrix style had only task IDs so far.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. XMPP STARTTLS matrix rows name their tests; XEP0368-1 and Surl-Tls-2 name BL-540
