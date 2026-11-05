# ADR-0087 — How Surl's DTLS 1.2 server handshakes for `coaps`

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-403. `Surl.Dtls` builds it: BL-412 the record layer (decisions 1, 8, 9), BL-413 the
  handshake messages, fragmentation and the PRF (decisions 5, 6, 9), BL-414 the certificate and
  RawPublicKey handshakes, the cookie and the timers (decisions 2, 3, 4, 7), BL-415 the PSK mode and
  the alerts (decisions 2, 10). The CoAP server's own task wires the options (decision 3).
- **Amends:** [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decision 3: `Surl.Dtls.UnitLibrary` may also reference `Surl.Cryptography.Curve25519.UnitLibrary`,
  for x25519 (decision 5 below); it still needs neither ChaCha20 nor Poly1305.
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) section 10:
  a `coaps` listen URL starts with a certificate *or* an account (decision 3 below).

## Context

`coaps` is CoAP over DTLS 1.2 on UDP port 5684 (RFC 7252 section 9.1). ADR-0084 decision 3 puts the
hand-built DTLS server in `Cryptography/Surl.Dtls.UnitLibrary`, over the existing `IDatagramFlow`
seam, one flow per remote endpoint, timed by `ExchangeContext.TimeProvider`. The base class library
has no DTLS and `SslStream` cannot run over datagrams; it has every primitive the suites below need:
`AesGcm`, `AesCcm` (an 8-byte tag is allowed), `ECDiffieHellman` (P-256, P-384), `ECDsa`, `RSA`,
`HMACSHA256`, `HMACSHA384`, `X509Certificate2`, `X509Chain` and `RandomNumberGenerator`. Surl already
hand-builds X25519 (`Surl.Cryptography.Curve25519`, ADR-0048).

What the specifications require of a CoAP server:

- RFC 7252 section 9.1.3.1, PreSharedKey mode: `TLS_PSK_WITH_AES_128_CCM_8` (0xC0A8) is mandatory.
- Sections 9.1.3.2 and 9.1.3.3, RawPublicKey and Certificate modes:
  `TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8` (0xC0AE) on secp256r1 is mandatory.
- RFC 6347 is DTLS 1.2: section 4.1 the record layer, 4.1.2.6 the replay window, 4.1.2.7 silent
  discard of invalid records, 4.2.1 the stateless cookie, 4.2.2 and 4.2.3 the handshake header and
  fragmentation, 4.2.4 flights and timers, 4.2.6 `Finished`, 4.2.8 a new handshake on an existing
  association.

What the Curl port's client offers, which says only what to accept and is no verdict (ADR-0083
decision 1): the port's `Coap-Dtls-Remaining.md`, row `Coap:RFC6347-4.3-1`, lists the AEAD suites
0xC02B, 0xC02F and 0xC0AE; its reference server uses 0xC02F with a self-signed RSA certificate.
Upstream curl has no `coaps`, so no byte here is measured from it; every byte comes from the RFCs
cited, as ADR-0083 requires of an extended server.

The certificate options of ADR-0010 section 3 and the accounts and `--self-signed` of ADR-0032
already exist. ADR-0006 sets the limits every server honours.

## Decision

### 1. Version: DTLS 1.2 only

- The server negotiates DTLS 1.2 (`0xFEFD`) and nothing else. A `ClientHello` whose
  `client_version` is below 1.2 (on the wire, numerically above `0xFEFD`, such as DTLS 1.0's
  `0xFEFF`) gets fatal alert `protocol_version` (RFC 5246 appendix E.1, RFC 6347 section 4.1). A
  client offering DTLS 1.3 through `supported_versions` with `legacy_version` `0xFEFD` is served
  DTLS 1.2: a server without 1.3 ignores `supported_versions` (RFC 8446 section 4.2.1, which
  RFC 9147 carries over to DTLS).
- Compression: `null` only (RFC 5246 section 7.4.1.2). A `ClientHello` whose list lacks `null` gets
  fatal `illegal_parameter`.
- DTLS 1.3 (RFC 9147) is not decided here; this ADR is the DTLS 1.2 that RFC 7252 names.

### 2. Cipher suites and their order

All AEAD, all through the BCL. The server picks the **first suite in its own order** below that the
client offers and that the server's credentials can serve, following RFC 5246 section 7.4.1.4's
latitude: the CoAP-mandated CCM_8 suites stay available to every client, while a client that also
offers a GCM suite gets the 16-byte tag.

| Order | Suite | Code | Credential | PRF hash | Why |
| --- | --- | --- | --- | --- | --- |
| 1 | `TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256` | 0xC02B | ECDSA certificate or raw key | SHA-256 | RFC 5289; offered by the port's client |
| 2 | `TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384` | 0xC02C | ECDSA certificate or raw key | SHA-384 | RFC 5289 |
| 3 | `TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8` | 0xC0AE | ECDSA certificate or raw key | SHA-256 | RFC 7251; mandated by RFC 7252 sections 9.1.3.2 and 9.1.3.3 |
| 4 | `TLS_ECDHE_ECDSA_WITH_AES_128_CCM` | 0xC0AC | ECDSA certificate or raw key | SHA-256 | RFC 7251 |
| 5 | `TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256` | 0xC02F | RSA certificate or raw key | SHA-256 | RFC 5289; offered by the port's client |
| 6 | `TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384` | 0xC030 | RSA certificate or raw key | SHA-384 | RFC 5289 |
| 7 | `TLS_PSK_WITH_AES_128_GCM_SHA256` | 0x00A8 | an account | SHA-256 | RFC 5487 |
| 8 | `TLS_PSK_WITH_AES_128_CCM_8` | 0xC0A8 | an account | SHA-256 | RFC 6655; mandated by RFC 7252 section 9.1.3.1 |
| 9 | `TLS_PSK_WITH_AES_128_CCM` | 0xC0A4 | an account | SHA-256 | RFC 6655 |

- **The credential decides which rows are live.** The process serves one certificate (ADR-0010
  section 3): an ECDSA key makes rows 1 to 4 live, an RSA key rows 5 and 6. PSK rows are live only
  while at least one account exists (decision 3). A client offering no live suite gets fatal
  `handshake_failure` (RFC 5246 section 7.4.1.3).
- **The throwaway certificate is RSA 2048** (ADR-0010 section 3, unchanged), so under
  `--self-signed` a client gets row 5 or 6 - what the port's reference server answers - and a client
  that offers only the mandated 0xC0AE needs an ECDSA P-256 `--cert`. The throwaway certificate is
  not changed for `coaps`: one certificate per process is ADR-0010's rule, and every secure
  listener shares it.
- **No CBC, no ChaCha20-Poly1305, no anonymous or static-RSA suite.** CBC suites need
  encrypt-then-MAC and carry the padding-oracle history RFC 7457 records; RFC 7525 section 4.2
  recommends the AEAD suites alone. ChaCha20-Poly1305 (RFC 7905) is offered by no client in scope,
  and ADR-0084's row then needs no ChaCha20 or Poly1305. Static RSA and `DH_anon` have no forward
  secrecy or no authentication.
- **AEAD nonces.** GCM: the 4-byte implicit salt from the key block and the 8-byte explicit nonce,
  sent in the record, equal to the record's 16-bit epoch and 48-bit sequence number (RFC 5288
  section 3, which allows the sequence number). CCM and CCM_8: the same construction (RFC 6655
  section 3). The additional data is RFC 5246 section 6.2.3.3's, with `seq_num` being epoch and
  sequence number (RFC 6347 section 4.1.2.1).

### 3. Credentials: certificate, RawPublicKey and PSK, and how each is configured

- **Certificate mode** takes the process certificate exactly as `https` does: `--cert`, `--key`,
  `--cert-type`, `--key-type` and `--pass` (ADR-0010 section 3), or the throwaway one under
  `--self-signed` (ADR-0032 section 10). The `Certificate` message carries the chain in file order
  (RFC 5246 section 7.4.2). RSA (2048 bits or more) and ECDSA P-256 and P-384 keys are served; an
  ECDSA key on another curve, or an Ed25519 or Ed448 key, is `CertificateProblem` (58) at start when
  a `coaps` URL is listened on, because RFC 8422's TLS 1.2 ECDSA suites are signed here only on the
  curves decision 5 serves.
- **RawPublicKey mode (RFC 7250)** is served with the same key, no option needed. When the client's
  `server_certificate_type` extension lists `RawPublicKey` (2) before `X509` (0), or lists only
  `RawPublicKey`, the server answers `server_certificate_type` `RawPublicKey` and sends the key's
  `SubjectPublicKeyInfo` alone in `Certificate` (RFC 7250 sections 3 and 4.2); the server follows
  the client's order. A list naming neither type gets fatal `unsupported_certificate` (RFC 7250
  section 4.2).
- **Client certificates** follow ADR-0010 section 5 unchanged: with `--cacert`, the server sends
  `CertificateRequest` (certificate types `ecdsa_sign` and `rsa_sign`, the signature schemes of
  decision 5, an empty CA list) on every certificate or RawPublicKey handshake, and requires a
  client certificate chaining to a `--cacert` anchor. Client raw public keys are not accepted: no
  option names the keys to trust, so a `client_certificate_type` listing no `X509` gets fatal
  `unsupported_certificate` when `--cacert` is given, and is ignored (the extension not answered)
  when it is not. No `CertificateRequest` is sent on a PSK handshake (RFC 4279 section 2).
- **PSK mode** takes its identities from the accounts, so secure-by-default holds and no key is
  built in (ADR-0032): `--user name:password` and `--user-file` accounts, the identity being the
  account name's UTF-8 bytes and the key the password's UTF-8 bytes, the way libcoap's client takes
  `-u identity -k key`. No `--user` or `--user-file` account means no PSK suite is live. An account
  with an empty password is not a PSK: an empty key authenticates nothing. No
  identity hint is sent, so the server sends no `ServerKeyExchange` on a PSK handshake (RFC 4279
  section 2). `Surl.Dtls` knows no accounts: it takes a caller-given lookup from identity bytes to
  key bytes, and the CoAP server composes it from the accounts.
- **Who the peer is.** A finished handshake reports to the CoAP server the PSK identity, the client
  certificate, or neither. Whether that is a login is the CoAP server's own ADR's decision.
- **Starting without a credential.** `coaps` is not one of `TlsSchemes.IsImplicitTls`'s schemes:
  `Surl.Networking` performs no TLS for it, `Surl.Dtls` does. ADR-0032 section 10 is amended for
  it: a `coaps` listen URL starts when there is a certificate (`--cert` or `--self-signed`) or at
  least one account; with neither, it is refused before any listener binds with
  `CertificateProblem` (58) and
  `surl: (58) <listen url> needs a certificate or an account: give --cert <file>, --self-signed, or --user name:password for a pre-shared key`.
  With accounts and no certificate, only the PSK rows are live.

### 4. The cookie exchange (RFC 6347 section 4.2.1)

- **Always on.** Every `ClientHello` without a valid cookie is answered with a `HelloVerifyRequest`
  and no state is kept: no handshake hash, no timer, nothing in the flow but the counters
  `Surl.Core` already keeps. There is no option to turn it off; it is the server's defence against
  being an amplifier and against spoofed-address state, and a `HelloVerifyRequest` is smaller than
  the `ClientHello` that asked for it.
- **The cookie** is the first 32 bytes of `HMAC-SHA256(secret, remote address || remote port ||
  client_version || random || session_id || cipher_suites || compression_methods)`, binding it to
  the endpoint and to the hello's fields, as section 4.2.1 suggests. A second `ClientHello` whose
  fields differ from those the cookie covers fails the check and gets a fresh `HelloVerifyRequest`,
  never an alert.
- **The secret** is 32 bytes from `RandomNumberGenerator`, behind an injected source so tests can fix
  it, made when the first `coaps` listener starts and never written anywhere. It is replaced every
  5 minutes by the `TimeProvider`, and the previous secret is still accepted, so a cookie lives
  between 5 and 10 minutes (section 4.2.1 recommends changing the secret and accepting the previous
  one for a while).
- **Bytes.** The `HelloVerifyRequest`'s `server_version` is DTLS 1.0, `0xFEFF`, as section 4.2.1
  says a DTLS 1.2 server should send regardless of the version it will negotiate. Its record
  repeats the `ClientHello` record's sequence number and its `message_seq` repeats the
  `ClientHello`'s, and the `ServerHello` that answers the second `ClientHello` takes that
  `ClientHello`'s `message_seq` (section 4.2.2), so the server needs no memory between the two.
  Neither the first `ClientHello` nor the `HelloVerifyRequest` enters the handshake hash (section
  4.2.6).

### 5. Key exchange, signatures and the extensions answered

The server answers exactly these extensions and ignores every other one as if absent (RFC 5246
section 7.4.1.4: a server never sends an extension the client did not offer).

| Extension | Answer |
| --- | --- |
| `supported_groups` (10) | Not echoed (RFC 8422 section 5.2). The ECDHE group is the first in the server's order secp256r1 (23), x25519 (29), secp384r1 (24) that the client lists; with none of them, fatal `handshake_failure`. An ECDHE client that sends no `supported_groups` gets secp256r1 (RFC 8422 section 4). secp256r1 comes first because RFC 7252 mandates it and every CoAP client has it. secp256r1 and secp384r1 are the BCL's `ECDiffieHellman`; x25519 is `Surl.Cryptography.Curve25519` (the amendment to ADR-0084 above). |
| `ec_point_formats` (11) | Answered with `uncompressed` only (RFC 8422 section 5.2). A client list without `uncompressed` gets fatal `illegal_parameter`. |
| `signature_algorithms` (13) | `ServerKeyExchange` is signed with the first scheme in the **client's** order that the key can make: for an ECDSA key the scheme of its curve (`ecdsa_secp256r1_sha256` 0x0403, `ecdsa_secp384r1_sha384` 0x0503); for an RSA key `rsa_pss_rsae_sha256` 0x0804, `rsa_pss_rsae_sha384` 0x0805, `rsa_pkcs1_sha256` 0x0401 or `rsa_pkcs1_sha384` 0x0501 (RFC 5246 section 7.4.1.4.1, RFC 8446 section 4.2.3 for the PSS code points in TLS 1.2). SHA-1 is never used (RFC 9155 section 2). A client that sends no `signature_algorithms`, which RFC 5246 would read as SHA-1, or offers none the key can make, gets fatal `handshake_failure`. Not needed on a PSK handshake. |
| `extended_master_secret` (23) | Answered when offered, and the master secret is then RFC 7627 section 4's, over the session hash. A client that does not offer it is still served with RFC 5246's master secret: without resumption (below) RFC 7627 section 5.3's abort is not needed. |
| `renegotiation_info` (65281) | Answered with an empty `renegotiated_connection` when offered, or when the `TLS_EMPTY_RENEGOTIATION_INFO_SCSV` (0x00FF) is in the suites (RFC 5746 section 3.6), so clients know renegotiation is not insecure - it is refused anyway (decision 6). |
| `server_certificate_type` (20), `client_certificate_type` (19) | Decision 3, RFC 7250. |
| `max_fragment_length` (1) | Honoured and echoed (RFC 6066 section 4): records the server sends then carry at most that many plaintext bytes, and its handshake fragments fit them. Constrained CoAP clients ask for it. |

- **Ignored, named so nobody wonders:** `server_name` (one certificate serves every name, ADR-0010
  section 3; not echoed), `session_ticket`, `encrypt_then_mac` (AEAD only, RFC 7366 section 3),
  `application_layer_protocol_negotiation` (CoAP over DTLS has no ALPN ID), `heartbeat`,
  `record_size_limit` (`max_fragment_length` covers the case), `supported_versions`,
  `padding`, and `connection_id` (RFC 9146): a connection ID lets one association move between
  remote endpoints, and the `IDatagramFlow` seam is one flow per endpoint, so declining it - which
  section 3 of RFC 9146 lets a server do - keeps the seam unchanged.
- **The PRF** is RFC 5246 section 5's, with HMAC-SHA256, or HMAC-SHA384 for rows 2 and 6. `Finished`
  carries 12 bytes of `verify_data` (section 7.4.9). The handshake hash covers each message as if
  sent in one fragment, with its 12-byte DTLS header (RFC 6347 section 4.2.6).

### 6. Renegotiation, resumption and a new hello

- **Renegotiation is refused.** A `ClientHello` arriving in epoch 1 on an established association
  gets a **warning** `no_renegotiation` and the association carries on (RFC 5246 section 7.2.2,
  RFC 5746). The server never sends `HelloRequest`. ADR-0006 section 4 refuses renegotiation over
  TLS for the same reason, CPU exhaustion.
- **No resumption.** The `ServerHello`'s `session_id` is empty, so no session is cached (RFC 5246
  section 7.4.1.3), and no ticket is issued. A `ClientHello` carrying a session ID gets a full
  handshake. This matches ADR-0082's TLS 1.3 server, which issues no tickets either.
- **A new hello on an existing association** - an epoch 0 `ClientHello` from an endpoint whose
  association is established, as when a client restarts - goes through the cookie exchange like any
  other; when it carries a valid cookie, the server starts the new handshake and drops the old
  association once the new one completes (RFC 6347 section 4.2.8), with no alert on the old one.
  Until then the old association's records are still accepted.

### 7. Flights, retransmission and the MTU (RFC 6347 sections 4.1.1 and 4.2.4)

- **Timer values.** The retransmission timer starts at **1 second** (section 4.2.4.1's recommended
  initial value), doubles at every expiry, and is capped at **60 seconds** (section 4.2.4.1); it
  returns to 1 second when a flight is answered. All on the injected `TimeProvider`.
- **What the server retransmits.** Its last flight, whole, when the timer expires while waiting for
  the client's next flight, or when the client retransmits its own previous flight (section 4.2.4).
  After the server's final flight (`ChangeCipherSpec` and `Finished`) it keeps that flight and
  sends it again whenever the client's final flight arrives again, until the first epoch 1
  application record arrives or the exchange ends (section 4.2.4, the last-flight rule).
- **How long it tries.** No retransmission count: the handshake is inside the head timeout
  (decision 9), which ends it. With the 30-second default that is retransmissions at 1, 3, 7 and
  15 seconds.
- **MTU.** The server sends datagrams of at most **1200 bytes**, fragmenting handshake messages to
  fit (section 4.2.3), and smaller when `max_fragment_length` asks. 1200 is QUIC's floor (RFC 9000
  section 14) and fits every IPv6 path (1280-byte minimum MTU) with room for UDP and IP headers.
  There is no path MTU discovery: section 4.1.1.1 allows a fixed conservative value, and CoAP sizes
  its own messages below it (RFC 7252 section 4.6). Received datagrams may be up to the transport's
  65,507 bytes; several records in one datagram are read in turn.

### 8. The record layer

- **Epochs** 0 (handshake, plaintext) and 1 (after `ChangeCipherSpec`), never more, since nothing
  renegotiates. **Sequence numbers** are 48-bit per epoch; an association whose next sequence
  number would wrap is closed with `close_notify` instead (RFC 6347 section 4.1).
- **Replay window**: 64 records, a sliding bitmap, always on (section 4.1.2.6, which points at
  RFC 4303 section 3.4.3's window of 64 as the default).
- **Invalid records are discarded silently** (section 4.1.2.7): a record that fails AEAD, belongs
  to an unknown epoch, is a replay, is shorter than its header says, or whose plaintext exceeds
  2^14 bytes (RFC 5246 section 6.2.1) is dropped with a verbose-log note and no alert. This is the
  section's recommended behaviour, and no alert is the only safe answer to a forged record.

### 9. ADR-0006's limits on a DTLS association

| ADR-0006 limit | How it applies |
| --- | --- |
| Concurrent connections and datagram flows (total, per address) | One association is one `IDatagramFlow`, counted by `Surl.Core` as today. A flow past a limit gets no reply - `Surl.Dtls` implements no `IDatagramRefusalWriter` - which is ADR-0006 section 5's "a secure refusal is a bare close" and section 4.2.1's rule against answering unverified addresses with more than a cookie. |
| Head timeout | Bounds the handshake: from the first `ClientHello` that carries a valid cookie (before it no state exists to time) to the server receiving the client's `Finished`. Hit: the association ends with no alert. |
| Idle timeout, maximum duration | `Surl.Core` cancels the exchange; the server sends one `close_notify` warning in the current epoch (RFC 5246 section 7.2.1) and the flow ends. This is DTLS's own close, as SSH's `DISCONNECT` is SSH's (ADR-0006 section 5). |
| `--max-message` | Bounds a reassembled handshake message (a client `Certificate` is the big one) and the fragments buffered for the current flight together. Hit: fatal `handshake_failure`, then the flow ends. Fragments of a later flight than the next one are dropped, not buffered. |
| `--max-request-head`, `--max-line`, `--max-filesize` | Not DTLS's: CoAP's own ADR applies `--max-filesize` to uploads; the other two do not apply. |
| Record size | Fixed by the RFC, not an option: 2^14 plaintext bytes (decision 8). |

A received `close_notify` is answered with `close_notify` and the association ends (RFC 5246
section 7.2.1). Hitting any limit never ends the process (ADR-0006 section 5).

### 10. The alert sent for each failure

All alerts are fatal unless marked, sent once in the current epoch, after which the association
ends and the flow is disposed. No alert is sent for an invalid record (decision 8) or a bad cookie
(decision 4).

| Failure | Alert (code) | Section |
| --- | --- | --- |
| `client_version` below DTLS 1.2 | `protocol_version` (70) | RFC 5246 appendix E.1 |
| Malformed handshake message, bad lengths, a fragment that contradicts an earlier one | `decode_error` (50) | RFC 5246 section 7.2.2 |
| A message the state machine does not expect | `unexpected_message` (10) | RFC 5246 section 7.2.2 |
| No live suite, no common group, no usable signature scheme, missing `signature_algorithms`, missing client certificate when `--cacert` requires one, `--max-message` exceeded | `handshake_failure` (40) | RFC 5246 sections 7.2.2 and 7.4.6 |
| No `null` compression, no `uncompressed` point format, a client ECDHE point not on the curve | `illegal_parameter` (47) | RFC 5246 section 7.2.2, RFC 8422 section 5.11 |
| No certificate type in common | `unsupported_certificate` (43) | RFC 7250 section 4.2 |
| Client certificate unreadable | `bad_certificate` (42) | RFC 5246 section 7.2.2 |
| Client certificate not chaining to a `--cacert` anchor | `unknown_ca` (48) | RFC 5246 section 7.2.2 |
| Client certificate expired or not yet valid by the `TimeProvider` | `certificate_expired` (45) | RFC 5246 section 7.2.2 |
| `CertificateVerify` does not verify; client `Finished` does not match | `decrypt_error` (51) | RFC 5246 section 7.2.2 |
| Unknown PSK identity | `decrypt_error` (51), reached by continuing with a random key so the client's `Finished` fails, never `unknown_psk_identity` | RFC 4279 section 2, which allows hiding the identity; ADR-0050 decision 5's rule that no peer learns which accounts exist |
| A `ClientHello` on an established association in epoch 1 | **warning** `no_renegotiation` (100) | RFC 5246 section 7.2.2 |
| Received `close_notify`; idle timeout or maximum duration | **warning** `close_notify` (0) | RFC 5246 section 7.2.1 |
| Head timeout | none | decision 9 |

A received fatal alert ends the association without a reply. A received warning alert other than
`close_notify` is logged and ignored.

**Amendment (2026-11-05, BL-415; decided by Claude under Stewart's delegation).** How the unknown
or wrong PSK reaches `decrypt_error`. Under an AEAD suite a client holding another key derives other
record keys, so its `Finished` record fails its tag and decision 8 would drop it silently: the
`Finished` comparison is never reached and the handshake would only end at the head timeout. So,
while the server waits for the client's `Finished`, an epoch 1 record that fails its tag ends the
handshake with fatal `decrypt_error` (51). The only epoch 1 record a client sends before the
server's `Finished` is its own `Finished`, so a failed tag there means the keys differ. An unknown
identity (keyed with a random key) and a wrong key end the same way, so the identity stays hidden.
RFC 6347 section 4.1.2.7 lets an implementation send an alert for an invalid record instead of
dropping it. It also opens no new attack: anyone who can inject that record from the client's
address could already end the handshake with a forged plaintext fatal alert. Decision 8's silent
drop still holds for every other invalid record, and for every record once the association is
established. Rejected: a timeout with no alert, because the client would hear nothing for the 30
seconds of the head timeout, and nothing would tell an operator why a wrong key failed.

**Amendment (2026-11-05, BL-738; decided by Claude under Stewart's delegation).** The details decisions
3, 5, 6 and 10 left open, as `Surl.Dtls` now serves them:

- **Certificate type lists.** `server_certificate_type` and `client_certificate_type` lists are read as
  RFC 7250 section 4.1's one-byte-length vector; the server answers the first type in the client's list it
  serves. A malformed list is `decode_error`, like any malformed extension.
- **A client certificate whose key is neither RSA nor ECDSA** (such as Ed25519) gets fatal
  `unsupported_certificate` (43): RFC 5246 section 7.2.2 names it for "a certificate of an unsupported
  type", and `CertificateVerify` could not be checked. A chain check failing because a certificate is
  outside its validity at the session's `TimeProvider` time is `certificate_expired`; any other chain
  failure is `unknown_ca`. The chain is built with the `--cacert` anchors as the only roots, the client's
  other certificates as intermediates, no revocation check and no download.
- **A `CertificateVerify` naming a scheme that does not match the key's kind**, or one outside
  `CertificateRequest`'s list, is `decrypt_error` (51), the same as a signature that does not verify: in
  both cases the signature does not prove possession of the key. Its signed content is every handshake
  message before it, each as if in one fragment (RFC 5246 section 7.4.8, RFC 6347 section 4.2.6).
- **`max_fragment_length`** whose data is not exactly one byte is `decode_error`; one byte outside 1 to 4
  is `illegal_parameter` (RFC 6066 section 4). The limit applies to every record from the server's first
  flight on, `ServerHello` included, and `DtlsServerSession.SendAsync` refuses application data longer
  than it with `ArgumentOutOfRangeException` rather than splitting it, because CoAP carries one message
  per record and the CoAP server sizes its messages by `MaximumApplicationDataLength`.
- **An epoch 1 `ClientHello`** is recognised by the handshake header at the start of an epoch 1 handshake
  record; each is answered with one warning `no_renegotiation` in epoch 1, and it never counts as the
  client's final flight sent again.
- **The epoch 0 hello on an established association** (decision 6's third bullet) is BL-779's.

## Alternatives considered

- **The client's suite order.** Rejected: CoAP clients often list the mandated CCM_8 first, and the
  server's order gives them a 16-byte tag whenever they also offer GCM, at no cost to a client that
  offers only CCM_8. ADR-0082 follows the client's order for TLS 1.3, where every suite is strong.
- **An ECDSA throwaway certificate for `coaps`.** Rejected: ADR-0010's one certificate per process
  would split, and the port's client and the port's reference server agree on RSA (0xC02F). An
  operator who needs 0xC0AE gives an ECDSA `--cert`.
- **A `--psk identity:hexkey` option.** Rejected: curl has no such option name, the accounts already
  carry an identity and a secret, and a second credential store would need its own secure-by-default
  rules.
- **`unknown_psk_identity` for an unknown identity.** Rejected: it tells a peer which identities
  exist; RFC 4279 section 2 offers the hiding answer.
- **Making the cookie optional.** Rejected: without it a spoofed source address costs the server a
  handshake's state and makes it an amplifier; section 4.2.1's SHOULD is followed always.
- **Session resumption.** Rejected for now: CoAP's DTLS sessions are long-lived associations, the
  full handshake on loopback costs milliseconds, and a session cache is state a peer can fill.
- **Connection IDs (RFC 9146).** Rejected: decision 5.

## Consequences

Good:

- A client following RFC 7252 in any of its three security modes completes a handshake: the
  mandated suites are always available with the matching credential.
- No new package and no new hand-built primitive: everything is the BCL or `Surl.Cryptography.Curve25519`.
- The server keeps no state for an unverified address, and every limit maps to a named answer.

Costs and caveats:

- Under `--self-signed` a client offering only 0xC0AE cannot connect; it needs an ECDSA `--cert`.
- PSK keys are passwords as UTF-8 bytes, so a binary key cannot be configured. A hex form, if a
  client in scope ever needs one, is a later ADR.
- The ADR-0032 section 10 refusal line for `coaps` is new text; the CoAP server's task pins it and
  its `--aihelp` facts.
