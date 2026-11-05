# Surl.Dtls.UnitLibrary

The server side of DTLS 1.2 (RFC 6347 over RFC 5246) for the `coaps` server, built by hand
because `SslStream` cannot run over datagrams. ADR-0084 decision 3 places it and fixes what it
may reference (`Surl.Protocol.Abstractions`, and `Surl.Cryptography.Curve25519` for x25519);
ADR-0087 is its specification. Where this file and those ADRs disagree, the ADRs win.

What it holds now (BL-412, the record layer, ADR-0087 decision 8):

- `DtlsRecordHeader` reads and writes the 13-byte record header: content type, version,
  16-bit epoch, 48-bit sequence number, length.
- `DtlsRecordProtection` is one direction's AEAD under ADR-0087's suites (`DtlsAeadAlgorithm`:
  AES-GCM, AES-CCM, AES-CCM_8), through the BCL's `AesGcm` and `AesCcm`. Nonce: the 4-byte
  salt then the 8-byte explicit nonce (epoch and sequence number), sent at the start of the
  fragment. Additional data: epoch and sequence number, type, version, plaintext length.
- `DtlsReplayWindow` is the 64-record sliding bitmap of RFC 6347 section 4.1.2.6; a record is
  marked only after its tag verifies.
- `DtlsRecordWriter` numbers and seals one epoch's records and refuses, with
  `DtlsSequenceNumberExhaustedException`, once 2^48 - 1 is used.
- `DtlsRecordReader` reads one epoch's records, several to a datagram, and drops every invalid
  one silently with a `DtlsRecordDropReason` for the verbose log: another epoch, a replay, a
  failed tag, a plaintext over 2^14 bytes, a truncated tail.
- `DtlsRecordLayer` is one association over its `IDatagramFlow`: epoch 0 plaintext, then
  epoch 1 under protection in each direction, once; it disposes the protections it is given.

The handshake messages, fragmentation and the PRF (BL-413, ADR-0087 decisions 5, 7 and 9):

- `DtlsHandshakeHeader` reads and writes the 12-byte handshake fragment header (RFC 6347
  section 4.2.2); `DtlsHandshakeMessage` is one whole message, and its `ToUnfragmentedBytes`
  is what the handshake hash covers (section 4.2.6).
- One record per message body, each with `Read` (throwing `DtlsDecodeErrorException`, which the
  handshake answers with `decode_error`) and `Write`: `DtlsClientHello` (with the cookie),
  `DtlsHelloVerifyRequest`, `DtlsServerHello`, `DtlsCertificate`, `DtlsRawPublicKeyCertificate`,
  `DtlsEcdheServerKeyExchange` (with `SignedContent` for the signature), `DtlsCertificateRequest`,
  `DtlsServerHelloDone`, `DtlsEcdheClientKeyExchange`, `DtlsPskClientKeyExchange`,
  `DtlsCertificateVerify` and `DtlsFinished`. Hello extensions are `DtlsExtension`s; a type
  appearing twice is a decode error. No PSK `ServerKeyExchange` exists: no identity hint is sent.
- `DtlsHandshakeFragmenter` splits a message to a caller-given record plaintext budget (the
  1200-byte datagram less record overhead, or `max_fragment_length`).
- `DtlsHandshakeReassembler` takes fragments in any order and overlap and hands whole messages
  over in `message_seq` order, reporting each fragment's `DtlsHandshakeFragmentOutcome`:
  duplicates and stale retransmissions are ignored, malformed or contradictory fragments are
  `decode_error`, and bodies held together past the `--max-message` limit are
  `handshake_failure`. Which flight a message belongs to is the handshake's concern, not its.
- `DtlsPseudorandomFunction` is RFC 5246 section 5's PRF over HMAC-SHA256 or -SHA384, with the
  master secret, the extended master secret (RFC 7627), the key block and both `Finished`
  `verify_data`s.

The certificate handshake, the cookie and the timers (BL-414, ADR-0087 decisions 2, 4, 5 and 7):

- `DtlsServerSession` is one association: `HandshakeAsync` waits, keeping no state, for a hello
  whose cookie verifies (`DtlsCookieExchange`, under the listener's `DtlsCookieSecrets`), then
  runs ECDHE under the caller's `DtlsServerCertificate` within `DtlsServerOptions.HandshakeTimeout`
  and reports a `DtlsHandshakeOutcome`; `ReceiveAsync` and `SendAsync` carry application data.
  The server's flight is sent again when the retransmission timer (1 s, doubling, capped at
  60 s, on the options' `TimeProvider`) fires or the client sends a stale message again; its
  final flight is sent again until the client's first application data.
- `DtlsServerNegotiation` picks the suite (`DtlsCipherSuite`, rows 1 to 6, CCM only where
  `AesCcmSupported`), the group (`DtlsNamedGroup`), the signature scheme and the extensions
  answered, and throws `DtlsAlertException` with the ADR's alert for each refusal.
- `DtlsEphemeralKey` is the server's ECDHE key on P-256, P-384 (BCL) or x25519
  (`Surl.Cryptography.Curve25519`); a bad client point is `illegal_parameter`.
- `DtlsAssociationRecords` reads epoch 0 and 1 side by side, one record at a time, and packs
  flights into datagrams of at most 1200 bytes, re-sealing each record when a flight is resent.
- The server's first flight continues from the verified hello's record sequence number, so its
  records never repeat the sequence number its `HelloVerifyRequest` reused.

The pre-shared-key mode, the alerts and closure (BL-415, ADR-0087 decisions 2, 3 and 10):

- `DtlsCipherSuite.PreSharedKeySuitesInServerOrder` is rows 7 to 9 (0x00A8, 0xC0A8 that RFC 7252
  mandates, 0xC0A4); `AllSuitesInServerOrder` is rows 1 to 9. A PSK suite has no
  `KeyAlgorithm` (`UsesPreSharedKey`). The PSK suites are live while `DtlsServerOptions.PreSharedKeys`,
  a caller-given `DtlsPreSharedKeyLookup` from identity bytes to key bytes, is set, and
  `DtlsServerOptions.Certificate` may then be `null` for a PSK-only server.
- A PSK handshake sends `ServerHello` and `ServerHelloDone` only: no `Certificate` and no
  `ServerKeyExchange`, because no identity hint is sent. The premaster secret is RFC 4279 section 2's.
- An unknown identity, or a lookup key that is empty or longer than 2^16 - 1 bytes, is keyed
  with a random key. While `Finished` is awaited, an epoch 1 record that fails its tag ends the
  handshake with `decrypt_error`, so a wrong key and an unknown identity end the same way and
  `unknown_psk_identity` is never sent (ADR-0087 decision 10 as amended).
- `CloseAsync` sends one warning `close_notify` on an established association (idle timeout,
  maximum duration). A received `close_notify` is answered with one, and a received fatal alert
  is not answered. Either way `ReceiveAsync` then returns `null` and `SendAsync` throws.

RawPublicKey, client certificates, `max_fragment_length` and renegotiation (BL-738, ADR-0087
decisions 3, 5, 6 and 10, and its BL-738 amendment):

- `DtlsServerNegotiation` answers `server_certificate_type` with the first of `X509` and
  `RawPublicKey` in the client's order (neither: `unsupported_certificate`); a RawPublicKey
  handshake sends `DtlsServerCertificate.SubjectPublicKeyInfo` alone in `Certificate`.
  `client_certificate_type` is answered `X509` only while a client certificate is asked for, and
  ignored otherwise. Neither is answered on a PSK handshake.
- With `DtlsServerOptions.ClientCertificateAuthorities` (`--cacert`), every certificate handshake
  sends `CertificateRequest` (`DtlsClientCertificate.Request`) and waits for the client's
  `Certificate`, then `ClientKeyExchange`, then `CertificateVerify`. `DtlsClientCertificate`
  checks the chain at the `TimeProvider`'s time (`handshake_failure` for none, `bad_certificate`,
  `unsupported_certificate`, `certificate_expired`, `unknown_ca`) and the `CertificateVerify`
  signature (`decrypt_error`).
- `max_fragment_length` codes 1 to 4 are echoed; `DtlsAssociationRecords.MaximumPlaintextLength`
  then cuts every handshake fragment, and `DtlsServerSession.SendAsync` refuses application data
  over `MaximumApplicationDataLength`.
- An epoch 1 `ClientHello` on an established association is answered with a warning
  `no_renegotiation`, and the association carries on.

Still to come: closing with `close_notify` before the 48-bit sequence number wraps (BL-745); an
epoch 0 hello with a valid cookie replacing an established association (BL-773); reporting the
peer (BL-650).

Never construct a socket here: records travel over the `IDatagramFlow` the caller gives.
Tests on macOS skip the AES-CCM cases (`OSCondition`), because the BCL's `AesCcm` support
there is not assured (`AesCcm.IsSupported`); coverage is measured on Windows. The handshake
must not offer a CCM suite where `AesCcm.IsSupported` is false.
