# Surl.Tls13.UnitLibrary

The TLS 1.3 handshake for Surl's hand-built QUIC (ADR-0082 decisions 2 and 3), built because
`SslStream` cannot hand QUIC its per-level secrets. QUIC does not use the library's record layer:
it carries the handshake messages in `CRYPTO` frames. The record layer is for TLS 1.3 over TCP
(ADR-0157), where `Surl.Tls13Stream` drives it and this handshake for a platform whose `SslStream`
has no TLS 1.3 server.

What it holds now (BL-375):

- `ClientHello.Read` reads a ClientHello (RFC 8446 section 4.1.2) and decodes the extensions a
  QUIC server acts on: `server_name`, `supported_versions`, `supported_groups`,
  `signature_algorithms`, ALPN (RFC 7301), `psk_key_exchange_modes`, `key_share` and
  `quic_transport_parameters` (opaque here; `Surl.Quic` parses them). Every extension, decoded or
  not, stays in `Extensions` in the client's order.
- `ServerHello` (and `ServerHello.HelloRetryRequest`), `EncryptedExtensions`,
  `CertificateRequest`, `CertificateMessage`, `CertificateVerify` and `Finished` write themselves
  from typed values; `TlsExtensions` builds the server's extensions.
- `TlsKeySchedule` is RFC 8446 section 7.1 without a pre-shared key, and `TlsTranscript` the
  running Transcript-Hash with the HelloRetryRequest `message_hash` of section 4.4.1. HKDF, HMAC
  and the SHA-2 hashes are the base class library's.

And the server handshake (BL-376):

- `TlsServerHandshake.Receive(level, bytes)` takes the client's handshake bytes per
  `TlsEncryptionLevel` as QUIC reassembles them (a message may span calls) and returns a
  `TlsHandshakeOutput`: the server's Initial bytes (ServerHello or HelloRetryRequest), its
  Handshake bytes (EncryptedExtensions, Certificate, CertificateVerify, Finished), and the
  handshake and application traffic secrets once known. It then checks the client's Finished.
  `CipherSuite`, `KeyExchangeGroup`, `ApplicationProtocol` and `ClientHello` (over QUIC, for the
  client's transport parameters) say what was negotiated. Over TCP, `Surl.Tls13Stream` passes it
  each handshake record's content: at the Initial level until the handshake secrets are out, at
  the Handshake level after.
- `TlsServerHandshakeOptions` carries the certificate chain and key (`RSA` signs
  rsa_pss_rsae_sha256, `ECDsa` the scheme of its P-256/P-384/P-521 curve), the ALPN protocols,
  the encoded `quic_transport_parameters`, any extensions EncryptedExtensions sends first, and
  the client-certificate check (below). An empty ALPN list means ALPN is neither checked nor
  answered; null transport parameters mean the extension is neither required nor sent.
  - QUIC (`Surl.Quic`'s `QuicServerConnection`) always passes transport parameters, and the ALPN
    list of its `QuicServerConnectionOptions`: `h3` by default, `doq` for DNS over QUIC.
  - TCP (`ServerTlsSettings.CreateTls13Options` in `Surl.Networking`) leaves the transport
    parameters null, adds no extensions, and passes the listener's ALPN list
    (`TlsApplicationProtocols`), which is empty for a listener that offers none.
- Choices, all ADR-0082 decision 3: the client's first supported suite; groups in the order
  X25519MLKEM768, x25519, secp256r1, secp384r1, secp521r1; with no share for one of them, a
  HelloRetryRequest for the first the client's `supported_groups` lists. X25519MLKEM768
  (draft-ietf-tls-ecdhe-mlkem) takes the client's 1216-byte share - the ML-KEM-768 encapsulation
  key, then an x25519 key - and answers 1120 bytes: `Surl.Cryptography.MlKem`'s ciphertext, then
  the server's x25519 key; the shared secret is ML-KEM's then x25519's. x25519 is
  `Surl.Cryptography.Curve25519`; the NIST curves are `ECDiffieHellman`, with the
  point checked on the curve by `TlsNistCurve` first, so a bad point is the same alert on every
  platform. Randomness comes from an injected `RandomNumberGenerator`, in share order: the server
  random, then for X25519MLKEM768 ML-KEM's 32-byte m, then an x25519 private key. No session ticket.

And the groups a curl offers only when `--curves` names one alone (BL-1008, ADR-0147), served after
those five in this order:

- SecP256r1MLKEM768 (`11EB`) and SecP384r1MLKEM1024 (`11ED`), draft-ietf-tls-ecdhe-mlkem (BL-1009):
  the client's share is the uncompressed NIST point then the ML-KEM-768 or ML-KEM-1024
  encapsulation key (1249 and 1665 bytes), the server's its point then the ciphertext (1153 and
  1665 bytes), and the secret the ECDH secret then ML-KEM's. The randomness is ML-KEM's m, after
  the server random; the NIST key is the base class library's `ECDiffieHellman`.
- curveSM2MLKEM768 (`11EE`), draft-yang-tls-hybrid-sm2-mlkem (BL-1030, ADR-0147): laid out like
  SecP256r1MLKEM768 with the curveSM2 point in the NIST curve's place (1249 bytes in, 1153 out).
- x448 (`001E`) through `Surl.Cryptography.Curve448` (BL-1010), RFC 7748 with the all-zero secret
  refused; the 56-byte private key comes from the injected `RandomNumberGenerator` after the
  server random.
- curveSM2 (`0029`), RFC 8998, through `Surl.Cryptography.Sm2` (BL-1030): an uncompressed 65-byte
  point, the secret the product's x-coordinate, a point off the curve refused; the 32-byte private
  key comes from the injected `RandomNumberGenerator` after the server random.
- ffdhe2048 (`0100`) and ffdhe3072 (`0101`), RFC 7919, in `TlsFiniteFieldGroup` with the base
  class library's `BigInteger.ModPow`: Y and the secret padded to the length of p, Y checked
  1 < Y < p - 1, and a 256- or 320-bit server exponent drawn from the injected
  `RandomNumberGenerator` after the server random.

And client certificates (BL-600, ADR-0082 decision 3 by way of ADR-0010 section 5):

- `TlsServerHandshakeOptions.TrustsClientCertificateChain` set means the server requests and
  requires a client certificate - ADR-0010 has no "request but do not require" mode. The
  CertificateRequest sits between EncryptedExtensions and Certificate, with an empty context and
  only `signature_algorithms`: ecdsa_secp256r1_sha256, ecdsa_secp384r1_sha384,
  ecdsa_secp521r1_sha512, rsa_pss_rsae_sha256, rsa_pss_rsae_sha384, rsa_pss_rsae_sha512 (every
  scheme the base class library verifies for an RSA or ECDSA certificate;
  `TlsCertificateVerifier.ClientSchemes`). No `certificate_authorities` list, as over TCP.
- The handshake then reads the client's Certificate, asks the delegate whether the chain is
  trusted (the caller builds the X.509 chain against `--cacert` with its own `TimeProvider`,
  since this library never reads a clock), verifies CertificateVerify with the end-entity key over
  "TLS 1.3, client CertificateVerify", and checks the client's Finished over a transcript that
  includes both. `ClientCertificateChain` holds the chain once CertificateVerify verifies.

And record protection (BL-1145, ADR-0157 decision 2), the record layer of RFC 8446 section 5 for a
TLS connection over TCP (QUIC has its own packet protection and does not use it):

- `TlsRecordProtector.Protect(type, content, paddingLength)` returns a complete record (outer type 23,
  version 0x0303), and `TlsRecordUnprotector.Unprotect(record)` returns a `TlsInnerPlaintext`
  (real content type, content without padding). Each takes the suite and one direction's traffic
  secret; the key and IV come from HKDF-Expand-Label `key` and `iv`, the nonce is the IV xor the
  64-bit sequence number, and `SequenceNumber` advances per record (unprotect advances only on a good tag).
- Suites: `TLS_AES_128_GCM_SHA256` and `TLS_AES_256_GCM_SHA384` with the base class library's
  `AesGcm`, `TLS_CHACHA20_POLY1305_SHA256` with `Surl.Cryptography.ChaCha20` and `Poly1305`
  (so this library references those two).
- `UpdateKeys()` derives the next secret ("traffic upd", section 7.2) and restarts the sequence at zero;
  the caller sends or reads the KeyUpdate message. A sequence number of 2^64 - 1 is refused.
- Limits: content at most 2^14, ciphertext at most 2^14 + 256 (`record_overflow`), a bad tag
  `bad_record_mac`, a wrong outer type `unexpected_message`, a length that does not fit `decode_error`.
  A compatibility change_cipher_spec record is not protected: the caller skips it before `Unprotect`.
- Tests replay the protected records of RFC 8448 section 3 byte for byte (`Rfc8448Records`).

Malformed input throws `TlsAlertException` carrying the RFC 8446 section 6 alert. This library
sends nothing on the wire; its caller reports the alert:

- Over QUIC, `Surl.Quic`'s `QuicServerConnection` sends no alert record: it closes the connection
  with `CONNECTION_CLOSE`, error `0x0100` plus the alert (RFC 9001 section 4.8).
- Over TCP, `Surl.Tls13Stream`'s `Tls13ServerStream` writes it as a fatal alert record, in
  plaintext before the handshake keys are installed and protected after, ignoring a connection
  already gone; the exception then reaches the stream's caller. The record layer's alerts
  (`TlsRecordUnprotector`, above) go the same way.

The table holds over both, except where a row says otherwise:

| Input | Alert |
| --- | --- |
| a length that overruns, a vector below its minimum (an empty cipher-suite list), trailing bytes | `decode_error` |
| a `legacy_version` that is not `0x0303` | `protocol_version` |
| a repeated extension, a group with two key shares, compression other than null, `pre_shared_key` not last | `illegal_parameter` |
| a message that is not the expected type, or at the wrong level or time; any byte at the 1-RTT level (`Surl.Tls13Stream` never passes that level) | `unexpected_message` |
| no TLS 1.3 in `supported_versions` | `protocol_version` |
| no `supported_groups` or `signature_algorithms`; no `quic_transport_parameters` when the server sends its own (QUIC only: TCP passes none) | `missing_extension` |
| no shared cipher suite, no offered scheme for the certificate key, no group the server serves | `handshake_failure` |
| when the server's ALPN list is not empty, none of it offered (or no ALPN at all): over QUIC `h3`, or `doq` for DNS over QUIC; over TCP the listener's list | `no_application_protocol` |
| a malformed key share, an ML-KEM encapsulation key failing FIPS 203's modulus check, a NIST or curveSM2 point off its curve, an x25519 or x448 low-order point, an ffdhe Y outside 1 < Y < p - 1; a second ClientHello changing the suite or not sending exactly one share for the retry group; a message over `MaximumMessageLength` (128 KiB) | `illegal_parameter` |
| a client Finished that does not verify | `decrypt_error` |
| an empty client Certificate when one is required | `certificate_required` |
| a client Certificate whose end-entity certificate is not X.509, or whose chain the delegate does not trust | `bad_certificate` |
| a client Certificate with a non-empty context; a client CertificateVerify scheme the CertificateRequest did not offer | `illegal_parameter` |
| a client CertificateVerify that does not verify, including a scheme for another key type or curve than the certificate's | `decrypt_error` |

After an alert the handshake is over: a later `Receive` throws `InvalidOperationException`.

It references `Surl.Cryptography.ChaCha20.UnitLibrary`,
`Surl.Cryptography.Curve25519.UnitLibrary`, `Surl.Cryptography.Curve448.UnitLibrary`,
`Surl.Cryptography.MlKem.UnitLibrary`, `Surl.Cryptography.Poly1305.UnitLibrary` and
`Surl.Cryptography.Sm2.UnitLibrary` (ADR-0082 decision 2, ADR-0147, ADR-0157 decision 2).
Bytes in, bytes out: never open a socket or a file, and never read a clock.

The expected values in `Surl.Tls13.UnitTests` come from these, never from the Curl port
(ADR-0003):

- RFC 8448: sections 2, 3 and 5 (`Rfc8448`) and section 3's records (`Rfc8448Records`).
- The primes of RFC 7919 appendix A.1 and A.2, copied into `UnitTestFiniteFieldPrimes`.
- ClientHellos recorded from the QUIC Initial packets of pinned upstream curl builds
  (`Fixtures/ClientHello`, and its `Curves` folder for each `--curves <group>` offer).
- Bytes, lengths, limits and alerts written into the tests from the specifications named in this
  file. One is derived: `TlsRecordProtectionTests`' next server application secret, RFC 8446
  section 7.2 applied to RFC 8448's secret with HKDF-Expand.
- Surl's own choices and contract, where no specification fixes the answer: ADR-0082 decision 3
  and ADR-0147's preferences, the message limit, and the .NET exceptions for caller errors.
- Values computed as the test runs. `UnitTestTlsClient`, a test-side TLS 1.3 client written from
  RFC 8446 that shares no code with the library's key schedule, derives the secrets and checks the
  server's flight; its key exchange uses the base class library and the same `Surl.Cryptography.*`
  primitives as the library. `UnitTestClientAuthentication` writes that client's Certificate,
  CertificateVerify and the Finished after them. `Surl.Cryptography.MlKem` and `Curve25519` give
  the X25519MLKEM768 share that scripted randomness fixes. The base class library's `AesGcm` and
  `ChaCha20Poly1305` give two expected records, from a key and IV the library's
  `TlsKeySchedule.ExpandLabel` derives (itself checked against RFC 8448).
- The tests' own inputs, read back.
