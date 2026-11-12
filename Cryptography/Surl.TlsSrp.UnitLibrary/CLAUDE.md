# Surl.TlsSrp.UnitLibrary

Holds Surl's hand-built TLS 1.2 server for RFC 5054's SRP cipher suites, decided in ADR-0122
decisions 1 to 3 and 6 and its Amendment 1. BL-565 built the record layer and the key derivation;
BL-566 built the handshake messages, the server's state machine and the alerts on top of them.
BL-911 added the RSA- and DSS-signed suites (`C0 1B`, `C0 1E`, `C0 21` and `C0 1C`, `C0 1F`, `C0 22`),
served only when `TlsSrpServer` is given a `TlsSrpServerCertificate`.

The signed suites (BL-911, ADR-0122 decisions 2 and 3):
- `TlsSrpServerCertificate(chain, key)` holds the DER chain and an RSA or DSA private key (this
  library reads no file). `SignedSuites` is the three RSA suites or the three DSS ones.
  `ChooseScheme` is the first `TlsSignatureScheme` in the client's `signature_algorithms` the key
  can make (RSA: PKCS#1 with SHA-1, -256 or -384, and RSA-PSS-RSAE with SHA-256 or -384; DSA with
  SHA-1, -256 or -384); with no extension, SHA-1 with the key's algorithm (`02 01` or `02 02`).
  `Sign` signs RSA through the BCL; for DSA it hashes and cuts the digest to the length of `q`
  itself, so a scheme's hash works on every platform.
- `TlsSrpNegotiation.Negotiate(hello, certificate)` serves the signed suites of the certificate's
  key only when a scheme was chosen; the suite is still the client's first servable one. Its
  `SignatureScheme` is null for an unsigned suite. No servable suite is `handshake_failure`; an
  empty or overlong `signature_algorithms` is `decode_error`.
- A signed suite's flight is ServerHello, `Certificate`, a ServerKeyExchange whose
  `ServerSRPParams` are followed by the scheme and the signature over
  `client_random + server_random + ServerSRPParams`, then ServerHelloDone. DSA tests are skipped on
  macOS, where the BCL has no DSA.

The handshake:
- `TlsSrpServer.AuthenticateAsync` runs the server side on the `Stream` it is handed and returns a
  `TlsSrpSession`. It reads the ClientHello, then sends ServerHello, ServerKeyExchange (`N`, `g`,
  `s`, `B`) and ServerHelloDone in one record. Then it reads ClientKeyExchange (`A`), the client's
  `ChangeCipherSpec` and its protected `Finished`, and sends its own `ChangeCipherSpec` and
  `Finished`. A fault sends its fatal alert in plaintext and throws the `TlsAlertException`. An
  alert from the client is a `TlsPeerAlertException`, and the server sends nothing back. A
  connection that ends during the handshake is an `EndOfStreamException`. One injected
  `RandomNumberGenerator` supplies, in order, the server random, an unknown user's simulated
  verifier, `b`, and every explicit IV.
- `TlsClientHello` reads a hello. `TlsSrpNegotiation` agrees the suite (the client's first of the
  three unsigned ones), the `srp` user name, `extended_master_secret`, `encrypt_then_mac`,
  `renegotiation_info` (or the `00 FF` signal), ALPN (`http/1.1` only) and SNI. It refuses the
  rest with the alerts ADR-0122 decision 6 lists. `TlsServerMessages` writes the server's
  messages.
- `TlsSrpAccounts` finds an account by the exact bytes of the user name. An unknown user gets a
  simulated `TlsSrpAccount`: a salt from HMAC-SHA256 under a secret drawn once, the 2048-bit group
  and a random verifier. The handshake then fails at the client's `Finished` with `bad_record_mac`,
  exactly as a wrong password does. Only the thrown `TlsAlertException`'s message tells them apart
  (`The TLS-SRP login failed: unknown user '<name>'.` or `... wrong password for '<name>'.`, ADR-0122
  Amendment 3), for the verbose log; the wire never does.
- `TlsHandshakeMessageReader` reassembles handshake messages across records. `TlsMessageReader`
  reads fixed fields and vectors, and refuses short or overlong input with `decode_error`.
  `TlsAlerts` writes and reads alert bodies.
- `TlsSrpStream` is the plaintext application data afterwards, and is asynchronous only. A
  ClientHello gets the warning `no_renegotiation`. `close_notify` or the end of the connection is
  the end of the stream. A fatal alert from the client throws. `ShutdownAsync` sends `close_notify`
  once and leaves the connection open, so `Surl.Networking` can half-close and linger (BL-567).
  `DisposeAsync` sends `close_notify` unless `ShutdownAsync` already did, and closes the connection.

The record layer:
- `TlsPseudorandomFunction` is RFC 5246 section 5's PRF with SHA-256 (every RFC 5054 suite uses
  it), with the master secret (RFC 5246 section 8.1), the extended master secret (RFC 7627
  section 4), the key block (section 6.3) and both `Finished` `verify_data` values (section 7.4.9).
  It is kept apart from `Surl.Dtls`'s `DtlsPseudorandomFunction` on purpose (ADR-0122 decision 1).
- `TlsSrpCipherSuite` names the nine suites. `TlsSrpCipherSuites` says which `TlsBulkCipher` each
  uses (3DES-EDE, AES-128 or AES-256, all CBC) and its key and block lengths. Every suite MACs with
  HMAC-SHA1.
- `TlsKeyBlock` cuts the key block into the client and server MAC keys and encryption keys, and
  makes each direction's protection. It cuts no IVs, because a TLS 1.2 CBC record carries an
  explicit IV.
- `TlsCbcRecordProtection` is one direction after `ChangeCipherSpec`: MAC-then-encrypt (RFC 5246
  section 6.2.3.2) or encrypt-then-MAC (RFC 7366). Every MAC covers the 64-bit sequence number, so
  a replayed or reordered record fails. A bad MAC and bad padding are refused alike with
  `bad_record_mac`. The MAC-then-encrypt padding check (`TlsCbcPadding`) does not branch on the
  padding's bytes (RFC 5246 section 6.2.3.2's note on timing).
- `TlsRecordReader` and `TlsRecordWriter` read and write records on a `Stream`, in plaintext until
  `ChangeProtection` and protected after it. The writer cuts a payload into records of at most
  2^14 bytes with version `03 03`. The reader refuses a plaintext record over 2^14 bytes or a
  protected one over 2^14 + 2048 with `record_overflow`, and an unknown content type with
  `unexpected_message`. It returns `null` when the stream ends between records.

It references `Surl.Cryptography.Srp.UnitLibrary` and nothing else (ADR-0002 decision 3's table,
as ADR-0122 amends it). It works on the `Stream` it is handed and never constructs a `Socket`,
`TcpListener`, `UdpClient`, `SslStream` or `HttpListener`. It never reads a clock.

Expected values come from the RFCs, the published TLS 1.2 PRF test vectors, formulas written out
by hand in the tests, and the bytes pinned upstream curl 8.21.0 sent, which `Record-CurlExchange.ps1
-TlsSrp` recorded (`Surl.TlsSrp.UnitTests/Fixtures`). They never come from the Curl port
(ADR-0003).
