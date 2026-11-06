# Surl.Tls13.UnitLibrary

The TLS 1.3 handshake for Surl's hand-built QUIC (ADR-0082 decisions 2 and 3), built because
`SslStream` cannot hand QUIC its per-level secrets. It has no record layer: QUIC carries the
handshake messages in `CRYPTO` frames.

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
  `CipherSuite`, `KeyExchangeGroup`, `ApplicationProtocol` and `ClientHello` (for the client's
  transport parameters) say what was negotiated.
- `TlsServerHandshakeOptions` carries the certificate chain and key (`RSA` signs
  rsa_pss_rsae_sha256, `ECDsa` the scheme of its P-256/P-384/P-521 curve), the ALPN protocols
  (`h3`), the encoded `quic_transport_parameters`, and any extensions EncryptedExtensions sends
  first. Empty ALPN or null transport parameters turn those checks off, which only the RFC 8448
  test (not QUIC) uses.
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

Malformed input throws `TlsAlertException` carrying the RFC 8446 section 6 alert; nothing is sent
on the wire, the caller reports it as QUIC error `0x0100` plus the alert (RFC 9001 section 4.8):

| Input | Alert |
| --- | --- |
| a length that overruns, a vector below its minimum (an empty cipher-suite list), trailing bytes | `decode_error` |
| a `legacy_version` that is not `0x0303` | `protocol_version` |
| a repeated extension, a group with two key shares, compression other than null, `pre_shared_key` not last | `illegal_parameter` |
| a message that is not the expected type, or at the wrong level or time; any byte at the 1-RTT level | `unexpected_message` |
| no TLS 1.3 in `supported_versions` | `protocol_version` |
| no `supported_groups` or `signature_algorithms`; no `quic_transport_parameters` when the server sends its own | `missing_extension` |
| no shared cipher suite, no offered scheme for the certificate key, no group the server serves | `handshake_failure` |
| no `h3` (or no ALPN at all) | `no_application_protocol` |
| a malformed key share, an ML-KEM-768 encapsulation key failing FIPS 203's modulus check, a NIST point off its curve, an x25519 low-order point; a second ClientHello changing the suite or not sending exactly one share for the retry group; a message over `MaximumMessageLength` (128 KiB) | `illegal_parameter` |
| a client Finished that does not verify | `decrypt_error` |
| an empty client Certificate when one is required | `certificate_required` |
| a client Certificate whose end-entity certificate is not X.509, or whose chain the delegate does not trust | `bad_certificate` |
| a client Certificate with a non-empty context; a client CertificateVerify scheme the CertificateRequest did not offer | `illegal_parameter` |
| a client CertificateVerify that does not verify, including a scheme for another key type or curve than the certificate's | `decrypt_error` |

After an alert the handshake is over: a later `Receive` throws `InvalidOperationException`.

It references `Surl.Cryptography.Curve25519.UnitLibrary` and
`Surl.Cryptography.MlKem.UnitLibrary` (ADR-0082 decision 2). Bytes in,
bytes out: never open a socket or a file, and never read a clock.

Every expected value in its tests comes from RFC 8448 (sections 3 and 5) or from ClientHellos
recorded from pinned upstream curl builds (`Fixtures/ClientHello`, BL-363's `-QuicInitial`),
never from the Curl port (ADR-0003). Handshakes are checked by `UnitTestTlsClient`, a test-side
TLS 1.3 client written from RFC 8446 that shares no code with the library's key schedule;
`UnitTestClientAuthentication` writes that client's Certificate, CertificateVerify and Finished.
