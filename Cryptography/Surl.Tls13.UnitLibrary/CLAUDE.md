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

Malformed input throws `TlsAlertException` carrying the RFC 8446 section 6 alert; nothing is sent
on the wire, the caller reports it as QUIC error `0x0100` plus the alert (RFC 9001 section 4.8):

| Input | Alert |
| --- | --- |
| a length that overruns, a vector below its minimum (an empty cipher-suite list), trailing bytes | `decode_error` |
| a `legacy_version` that is not `0x0303` | `protocol_version` |
| a repeated extension, a group with two key shares, compression other than null, `pre_shared_key` not last | `illegal_parameter` |
| a message that is not the expected type | `unexpected_message` |

It may reference `Surl.Cryptography.Curve25519.UnitLibrary` and
`Surl.Cryptography.MlKem.UnitLibrary` (ADR-0082 decision 2) and references neither yet: the key
exchange and the server handshake arrive with BL-376 and BL-544. Bytes in, bytes out: never open a
socket or a file, and never read a clock.

Every expected value in its tests comes from RFC 8448 (sections 3 and 5) or from ClientHellos
recorded from pinned upstream curl builds (`Fixtures/ClientHello`, BL-363's `-QuicInitial`),
never from the Curl port (ADR-0003).
