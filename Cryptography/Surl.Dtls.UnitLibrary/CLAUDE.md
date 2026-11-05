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

Still to come: the certificate and RawPublicKey handshakes, the cookie and the timers (BL-414),
PSK and alerts (BL-415).

Never construct a socket here: records travel over the `IDatagramFlow` the caller gives.
Tests on macOS skip the AES-CCM cases (`OSCondition`), because the BCL's `AesCcm` support
there is not assured (`AesCcm.IsSupported`); coverage is measured on Windows. The handshake
must not offer a CCM suite where `AesCcm.IsSupported` is false.
