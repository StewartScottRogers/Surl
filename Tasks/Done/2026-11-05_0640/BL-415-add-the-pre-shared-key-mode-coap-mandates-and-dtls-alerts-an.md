---
id: BL-415
title: Add the pre-shared-key mode CoAP mandates and DTLS alerts and closure to Surl.Dtls
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-414]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests, Documentation/Planning/Decisions/ADR-0087-how-surls-dtls-1-2-server-handshakes-for-coaps.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-415 — Add the pre-shared-key mode CoAP mandates and DTLS alerts and closure to Surl.Dtls

## Goal

A `Surl.Dtls` server session also completes a pre-shared-key handshake under `TLS_PSK_WITH_AES_128_CCM_8`
(RFC 7252 section 9.1.3.1). It looks the identity up through a caller-given lookup, sends and honours
`close_notify`, and sends the alerts the ADR names for each failure.

## Context

- Decision: BL-403's ADR (PSK configuration, the alert per failure). RawPublicKey (RFC 7250) is built here
  too if the ADR chose it.
- RFC 4279 sections 2 and 5 (PSK key exchange, the premaster secret, `unknown_psk_identity`), RFC 6655 (the
  CCM_8 suites), RFC 6347 section 4.1.2.7 and RFC 5246 section 7.2 (alerts, `close_notify`).
- The identity lookup is a delegate or interface the caller supplies (the CoAP server wires it to the accounts
  file). This library knows no accounts.

## Acceptance criteria

- [x] Named tests show:
      - a PSK handshake completed and application data exchanged;
      - an unknown identity ending with `unknown_psk_identity`;
      - a wrong key ending with `decrypt_error` or the ADR's alert;
      - `close_notify` sent on close and honoured on receipt;
      - every alert the ADR lists sent for its failure.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

What was built (2026-11-05):
- PSK suites 0x00A8, 0xC0A8 and 0xC0A4 (`DtlsCipherSuite.PreSharedKeySuitesInServerOrder`, rows 7 to 9). They are
  live when `DtlsServerOptions.PreSharedKeys`, a caller-given `DtlsPreSharedKeyLookup`, is set.
  `DtlsServerOptions.Certificate` is now nullable, for a server with accounts and no certificate.
  `DtlsServerNegotiation` reads no ECDHE extension on a PSK suite. The PSK flight is `ServerHello` and
  `ServerHelloDone` only, and the premaster secret is RFC 4279 section 2's.
- `DtlsServerSession.CloseAsync` sends one warning `close_notify`. A received `close_notify` is answered with one,
  and a received fatal alert (a fatal `close_notify` included) is not answered.

Acceptance tests:
- PSK: `DtlsServerSessionPreSharedKeyTests.HandshakeAsync_PskGcmSuite_CompletesAndExchangesApplicationData`
  and `HandshakeAsync_PskCcmSuites_CompleteAndExchangeApplicationData` (0xC0A8, 0xC0A4; skipped on macOS).
- Unknown identity: `HandshakeAsync_UnknownPskIdentity_EndsWithDecryptErrorNeverUnknownPskIdentity`.
  **Deliberate difference from the criterion's wording:** ADR-0087 decision 10, which this task's Context names as
  the decision, says an unknown identity gets `decrypt_error` and never `unknown_psk_identity`, so that no peer
  learns which accounts exist. The library's CLAUDE.md says the ADR wins, so the test pins `decrypt_error`.
- Wrong key: `HandshakeAsync_WrongPskKey_EndsWithDecryptError`.
- `close_notify`: `DtlsServerSessionApplicationDataTests.CloseAsync_Established_SendsOneCloseNotifyWarningAndEndsTheAssociation`,
  `ReceiveAsync_CloseNotify_IsAnsweredWithOneCloseNotifyAndEndsTheAssociation` and
  `ReceiveAsync_FatalAlert_ReturnsNullAndIsNotAnswered`.
- Alerts, at the session level: `protocol_version`, `illegal_parameter` (no null compression, a bad ECDHE point),
  `decode_error`, `unexpected_message`, `handshake_failure` and `decrypt_error` are each in
  `DtlsServerSessionFailureTests` or `DtlsServerSessionPreSharedKeyTests`. `bad_certificate`, `unknown_ca`,
  `certificate_expired`, `unsupported_certificate` and `no_renegotiation` belong to client certificates,
  RawPublicKey and renegotiation, which are BL-738's.
- Surl.Dtls.UnitTests: 225 tests green. Surl.Dtls.UnitLibrary: 100% line and 100% branch coverage (MSTest
  coverage collector, Cobertura).

Decision (ADR-0087 decision 10, amended in place, decided by Claude under Stewart's delegation): under AEAD, a wrong
or unknown PSK makes the client's `Finished` record fail its tag. Decision 8 would then drop it silently, and the
handshake would only time out. So while `Finished` is awaited, an epoch 1 record that fails its tag ends the
handshake with `decrypt_error`. A test found this: the in-memory client and server resent flights to each other
with no time passing until the test host ran out of memory.

Choices taken as sensible defaults (rule 1):
- The key for an unknown identity is 32 random bytes. A lookup key that is empty, or longer than RFC 4279's
  2^16 - 1 bytes, counts as unknown.
- `CloseAsync` before the handshake completes sends nothing. The head timeout governs that phase with no alert
  (decision 9).
- The lookup is a delegate (`DtlsPreSharedKeyLookup`), not an interface: one method, and the CoAP server composes
  it from its accounts.

Touches: added `Documentation/Planning/Decisions/ADR-0087-how-surls-dtls-1-2-server-handshakes-for-coaps.md` for
the amendment. No task in Doing names it.

Follow-up filed: BL-745 (`close_notify` before the 48-bit sequence number wraps, ADR-0087 decision 8).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Dtls completes PSK handshakes (0x00A8, 0xC0A8, 0xC0A4) through a caller-given identity lookup, ends wrong or unknown keys with decrypt_error, and sends and answers close_notify
