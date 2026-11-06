# Surl.TlsSrp.UnitLibrary

Holds Surl's hand-built TLS 1.2 server for RFC 5054's SRP cipher suites, decided in ADR-0122
decisions 1 to 3. BL-565 built the record layer and the key derivation; BL-566 adds the handshake
messages, the server's state machine and the alerts on top of them.

What is here now:
- `TlsPseudorandomFunction` is RFC 5246 section 5's PRF with SHA-256 (every RFC 5054 suite uses
  it), with the master secret (RFC 5246 section 8.1), the extended master secret (RFC 7627
  section 4), the key block (section 6.3) and both `Finished` `verify_data` values (section 7.4.9).
  It is kept apart from `Surl.Dtls`'s `DtlsPseudorandomFunction` on purpose (ADR-0122 decision 1).
- `TlsSrpCipherSuite` names the nine suites; `TlsSrpCipherSuites` says which `TlsBulkCipher`
  each uses (3DES-EDE, AES-128 or AES-256, all CBC) and its key and block lengths; every suite
  MACs with HMAC-SHA1.
- `TlsKeyBlock` cuts the key block into the client and server MAC keys and encryption keys (no
  IVs: a TLS 1.2 CBC record carries an explicit IV) and makes each direction's protection.
- `TlsCbcRecordProtection` is one direction after `ChangeCipherSpec`: MAC-then-encrypt (RFC 5246
  section 6.2.3.2) or encrypt-then-MAC (RFC 7366), the 64-bit sequence number in every MAC (so a
  replayed or reordered record fails), and a bad MAC and bad padding refused alike with
  `bad_record_mac`. The MAC-then-encrypt padding check (`TlsCbcPadding`) does not branch on the
  padding's bytes (RFC 5246 section 6.2.3.2's note on timing).
- `TlsRecordReader` and `TlsRecordWriter` read and write records on a `Stream`: plaintext until
  `ChangeProtection`, then protected. The writer fragments a payload into records of at most 2^14
  bytes with version `03 03`; the reader refuses a plaintext record over 2^14 bytes or a protected
  one over 2^14 + 2048 with `record_overflow`, an unknown content type with `unexpected_message`,
  and returns `null` when the stream ends between records.
- A fatal alert is a `TlsAlertException` carrying its `TlsAlertDescription`.

It may reference `Surl.Cryptography.Srp.UnitLibrary` and nothing else (ADR-0002 decision 3's
table, as ADR-0122 amends it); BL-566 adds that reference when the handshake needs it. It works on
the `Stream` it is handed and never constructs a `Socket`, `TcpListener`, `UdpClient`, `SslStream`
or `HttpListener`, never reads a clock, and draws every explicit IV from an injected
`RandomNumberGenerator` so tests can pin record bytes.

Expected values come from the RFCs, the published TLS 1.2 PRF test vectors and formulas written
out by hand in the tests, never from the Curl port (ADR-0003).
