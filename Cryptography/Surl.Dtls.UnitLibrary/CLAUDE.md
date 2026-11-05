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

Still to come: handshake messages and fragmentation (BL-413), the certificate handshake and the
TLS 1.2 PRF (BL-414), PSK and alerts (BL-415).

Never construct a socket here: records travel over the `IDatagramFlow` the caller gives.
Tests on macOS skip the AES-CCM cases (`OSCondition`), because the BCL's `AesCcm` support
there is not assured (`AesCcm.IsSupported`); coverage is measured on Windows. The handshake
must not offer a CCM suite where `AesCcm.IsSupported` is false.
