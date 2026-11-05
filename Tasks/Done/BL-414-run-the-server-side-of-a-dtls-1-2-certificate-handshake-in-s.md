---
id: BL-414
title: Run the server side of a DTLS 1.2 certificate handshake in Surl.Dtls
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-413]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-414 — Run the server side of a DTLS 1.2 certificate handshake in Surl.Dtls

## Goal

A `Surl.Dtls` server session completes a DTLS 1.2 handshake over an `IDatagramFlow`. It answers a cookieless
ClientHello with a `HelloVerifyRequest`, then runs ECDHE with the server's certificate under the suites the ADR
chose, and finishes with `Finished`. It retransmits lost flights on a `TimeProvider` timer, then exchanges
application data.

## Context

- Decision: BL-403's ADR (suites, certificate mode, cookie secret, timers, alerts).
- RFC 6347 section 4.2.1 (the stateless cookie: the server keeps no state before the cookie returns), section
  4.2.4 (flights, timers, retransmission; Figure 1's state machine), and section 4.2.6 (Finished).
  RFC 5246 section 7.4.3 (ServerKeyExchange signed with the certificate's key).
- Time: an injected `TimeProvider` only; tests drive it with the test project's `ManualTimeProvider`. No
  `Thread.Sleep`.
- The certificate comes from the caller (Surl's `--cert`/`--key`, ADR-0010). This library does not read files.

## Acceptance criteria

- [x] Named tests, driving the session through an in-memory `IDatagramFlow` with a test client written in the test
      project from RFC 6347, show:
      - a cookieless ClientHello answered with `HelloVerifyRequest` and nothing kept;
      - a full handshake completed under each chosen certificate suite, with application data exchanged both ways;
      - a lost server flight retransmitted once the timer fires;
      - a bad cookie ignored;
      - a bad Finished ending with the ADR's alert;
      - a handshake over the time limit abandoned.
- [x] `Documentation/Wiki/Glossary.md` defines DTLS, flight, cookie exchange and epoch.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

What was built (2026-11-05): `DtlsServerSession` (cookie exchange, ECDHE certificate handshake, retransmission,
application data), `DtlsServerNegotiation`, `DtlsCookieSecrets`/`DtlsCookieExchange`, `DtlsEphemeralKey` (P-256,
P-384, x25519), `DtlsServerCertificate`, `DtlsAssociationRecords`, `DtlsAlert`. `Surl.Dtls.UnitLibrary` now
references `Surl.Cryptography.Curve25519.UnitLibrary`, as ADR-0087's amendment of ADR-0084 allows. The test client
is `UnitTestDtlsClient` over `UnitTestDatagramFlow` with `ManualTimeProvider`.

Acceptance tests, in `DtlsServerSessionTests`: `HandshakeAsync_CookielessHello_AnswersHelloVerifyRequestAndKeepsNothing`,
`HandshakeAsync_EachGcmCertificateSuite_CompletesAndExchangesApplicationData` (0xC02B, 0xC02C, 0xC02F, 0xC030),
`HandshakeAsync_EachCcmCertificateSuite_CompletesAndExchangesApplicationData` (0xC0AE, 0xC0AC; skipped on macOS),
`HandshakeAsync_ServerFlightLost_RetransmitsItWhenTheTimerFires`,
`HandshakeAsync_BadCookie_IsAnsweredWithAFreshHelloVerifyRequestAndNoAlert`,
`HandshakeAsync_BadFinished_EndsWithDecryptError`, `HandshakeAsync_OverTheTimeLimit_IsAbandonedWithoutAnAlert`.
Surl.Dtls.UnitTests: 204 tests; Surl.Dtls.UnitLibrary measured at 100% line and 100% branch coverage with the
MSTest coverage collector (Cobertura).

Choices within ADR-0087, taken as sensible defaults (rule 1):
- The cookie's HMAC input writes each field as on the wire (with its length prefix) after the remote endpoint's
  serialized `SocketAddress`, so no two field splits give the same bytes.
- Before a cookie verifies, only the first record of a datagram is looked at, and anything but a whole, well-formed
  epoch 0 `ClientHello` (another type or epoch, a fragmented or malformed hello) is ignored with no answer: no
  address is verified yet, so no alert is sent and nothing is kept.
- The server's epoch 0 records after the cookie continue from the verified hello's record sequence number (the
  stateless server's way, as OpenSSL's `DTLSv1_listen` does), so no later record repeats the sequence number the
  `HelloVerifyRequest` reused. A test caught the alternative: an alert numbered 0 was dropped by the client as a
  replay of the `HelloVerifyRequest`.
- The retransmission timer restarts on each datagram received while waiting (1 s, doubling at each expiry, capped at
  60 s), rather than keeping a fixed deadline per flight; the head timeout bounds the whole handshake either way.
- A `ChangeCipherSpec` before `ClientKeyExchange` is ignored rather than refused, since reordering can bring it
  early and the client resends its flight; a `ChangeCipherSpec` that is not the single byte 1 is `decode_error`.
- Records of a datagram are read one at a time, so the `ChangeCipherSpec` starts epoch 1 reads before the
  `Finished` behind it is read; a test caught the eager version, which dropped every `Finished` and recovered only
  through a retransmission.
- A non-empty `renegotiation_info` in the initial hello is `handshake_failure` (RFC 5746 section 3.6).
- During the handshake a warning alert is ignored and a fatal one ends it (`AlertReceived`); once established any
  fatal alert or `close_notify` ends `ReceiveAsync` with `null`. Replying `close_notify` and the full alert list are
  BL-415's.

Left for other tasks: PSK and the rest of the alerts (BL-415); RawPublicKey, client certificates,
`max_fragment_length` and the renegotiation refusal (BL-725, filed by this task); reporting the peer (BL-650).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Dtls completes a DTLS 1.2 certificate handshake: stateless cookie, ECDHE under suites 0xC02B/C02C/C0AE/C0AC/C02F/C030, timer retransmission, Finished checks and application data
