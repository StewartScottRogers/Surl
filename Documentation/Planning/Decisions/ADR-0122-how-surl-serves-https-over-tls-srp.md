# ADR-0122 — How Surl serves HTTPS over TLS-SRP

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-563. Built by BL-564 (decision 1's SRP-6a library and decision 4's verifier files),
  BL-565 (decision 1's record layer and key derivation), BL-566 (decisions 2, 3 and 6's alerts),
  BL-567 (decisions 4, 5 and 6's command line) and BL-568 (decision 7).
- **Amends:** [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3: its table gains
  decision 1's two rows. [ADR-0010](ADR-0010-the-server-side-tls-contract.md) sections 3 and 4, and
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)'s need for
  `--cert` or `--self-signed`, for a process started with TLS-SRP (decisions 2, 5 and 6 below). [ADR-0030](ADR-0030-static-curl-8-21-0-windows-build-as-a-supplementary-build-for-smb.md):
  the supplementary static Windows build is also used to measure TLS-SRP's ClientHello (below).

## Context

Upstream curl's `--tlsauthtype SRP --tlsuser <user> --tlspassword <password>` authenticates a TLS
connection with a password instead of a certificate, through the Secure Remote Password cipher
suites of RFC 5054 (curl 8.21.0's manpage, https://curl.se/docs/manpage.html: `--tlsauthtype`,
`--tlsuser`, `--tlspassword`, and `--proxy-tlsauthtype` and friends for an HTTPS proxy; with
`--tlsuser` and `--tlspassword` and no `--tlsauthtype`, the type defaults to `SRP`). `SslStream`
offers no SRP cipher suite on any platform, so Surl's side of it is hand-built (root `CLAUDE.md`,
"Decisions").

[ADR-0080](ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md) decision 2 marks
the `httptls+srp` and `httptls+srp-ipv6` servers `server-not-built`. Four upstream cases wait on
this ADR (tag `curl-8_21_0`, commit `68720b4837284335b2d63cb358f8f6ce65f5bc55`, classified by
BL-399), all `httptls+srp`, all with feature `TLS-SRP`; `httptls+srp-ipv6` has none at the tag:

| Case | Command (after `https://%HOSTIP:%HTTPTLSPORT`) | Verified |
| --- | --- | --- |
| 320 | `--insecure --tlsauthtype SRP --tlsuser jsmith --tlspassword abc -A curl-test-suite` | stdout, after `<stripfile>`: `HTTP/1.0 200 OK`, `Content-type: text/html`, blank, `FINE`, `User-Agent: curl-test-suite`, `Accept: */*`, blank |
| 321 | `--insecure --tlsauthtype SRP --tlsuser baduser --tlspassword badpass` | exit 35 |
| 322 | `--insecure --tlsauthtype SRP --tlsuser jsmith --tlspassword badpass` | exit 35 |
| 324 | `--tlsauthtype SRP --tlsuser jsmith --tlspassword abc` (no `--insecure`), path `/want/fails` | exit 60 |

Upstream's own server for them is `gnutls-serv --http --priority NORMAL:+SRP --srppasswd
certs/srp-verifier-db --srppasswdconf certs/srp-verifier-conf` (`tests/servers.pm`,
`runhttptlsserver`, at the tag): SRP credentials only, no certificate. `tests/certs/srp-verifier-db`
holds GnuTLS `tpasswd` lines for `jsmith` and `alice`; `tests/certs/srp-verifier-conf` holds three
GnuTLS `tpasswd.conf` groups, each with generator 2. ADR-0080's import copies only the `.prm` files
and `test-ca.cnf` of `tests/certs`, so neither file is in the repository yet (decision 7).

**Which pins can run the cases.** The Linux and macOS reference pins report `TLS-SRP`
(`UpstreamCurlBuilds.json`; [ADR-0016](ADR-0016-the-linux-and-macos-upstream-curl-builds-and-how-ci-obtains-them.md)):
`/opt/upstream-curl/8.21.0/curl`, SHA-256
`153CA463957609117D21A848BE29B70691B85F9E5CC9370C7DAA037B839A4E45` (linux-x64), and
`/opt/upstream-curl/8.21.0/curl`, SHA-256
`04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF` (osx-arm64). The Windows
reference pin is Schannel and lacks the feature, so there 320, 321, 322 and 324 skip
`feature-missing:TLS-SRP` (ADR-0080 decision 3). **The cases run on the Linux and macOS reference
pins only.**

### What upstream curl 8.21.0 sends with `--tlsauthtype SRP`

Neither Linux nor macOS pin runs on the machine that made this decision, but the supplementary
static Windows build is the same unpatched tag statically linked against the same OpenSSL 4.0.1
as both of them, and reports `TLS-SRP`: `C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`,
SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`. Measured on
2026-11-05 with

```
.\Record-CurlExchange.ps1 -Port 18563 -Raw -RawIdleMilliseconds 1500 `
  -Curl 'C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe' `
  -CurlArgs '-sS,-k,--max-time,5,--tlsauthtype,SRP,--tlsuser,jsmith,--tlspassword,abc,https://127.0.0.1:18563/' `
  -OutDirectory <dir>
```

(no reply sent; curl exits 35, `TLS connect error: error:0A000126:SSL routines::unexpected eof
while reading`). Its one ClientHello, a 1512-byte handshake message in one record of version `03 01`:

- `client_version` `03 03`, a 32-byte random, a 32-byte legacy session ID, compression `null` only.
- **Cipher suites, in order:** `13 02`, `13 03`, `13 01` (TLS 1.3's), then all nine of RFC 5054
  section 2.7's: `C0 22` SRP_SHA_DSS_AES_256, `C0 21` SRP_SHA_RSA_AES_256, `C0 20` SRP_SHA_AES_256,
  `C0 1F` SRP_SHA_DSS_AES_128, `C0 1E` SRP_SHA_RSA_AES_128, `C0 1D` SRP_SHA_AES_128, `C0 1C`
  SRP_SHA_DSS_3DES, `C0 1B` SRP_SHA_RSA_3DES, `C0 1A` SRP_SHA_3DES. No other TLS 1.2 suite (curl
  sets OpenSSL's cipher list to `SRP` when none is given) and no `00 FF` signalling value.
- **Extensions, in order:** `renegotiation_info` (`ff 01`, empty); `srp` (`00 0c`, `06 6a 73 6d 69
  74 68`, the user name `jsmith`); `supported_groups` (X25519MLKEM768, SecP256r1MLKEM768,
  SecP384r1MLKEM1024, x25519, secp256r1, x448, secp384r1, secp521r1, curveSM2, ffdhe2048,
  ffdhe3072); `encrypt_then_mac` (`00 16`); `extended_master_secret` (`00 17`);
  `post_handshake_auth` (`00 31`); `signature_algorithms` (27 schemes, ML-DSA first, then ECDSA,
  EdDSA, RSA-PSS, `04 01`/`05 01`/`06 01` RSA PKCS#1, SHA-1 and SHA-224 ones, and `04 02`/`05 02`/
  `06 02` DSA); `supported_versions` (`03 04`, `03 03`); `psk_key_exchange_modes` (psk_dhe_ke);
  `key_share` (X25519MLKEM768 and x25519); `application_layer_protocol_negotiation` (`h2`,
  `http/1.1`). No `server_name` for an IP-literal host, no `session_ticket`, no
  `ec_point_formats`, no `status_request`.

BL-566 records the full exchange against its server on the same build, and BL-568's CI legs observe
the Linux and macOS pins themselves; a difference from the bytes above reopens this ADR.

### What curl does with the server's choice

From curl 8.21.0's `lib/vtls/openssl.c` and OpenSSL 4.0.1's `ssl/tls_srp.c`, to be confirmed by
BL-566's measurement: with `--tlsauthtype SRP` curl hands the user and password to OpenSSL, whose
client accepts only a group it knows (RFC 5054 Appendix A; RFC 5054 section 3.2) of at least 1024
bits and otherwise sends `insufficient_security`; a failed handshake is `CURLE_SSL_CONNECT_ERROR`
(35); and with peer verification on (no `--insecure`), a server that sent no certificate is
`CURLE_PEER_FAILED_VERIFICATION` (60), `SSL: could not get peer certificate`. That is case 324:
gnutls-serv has no certificate and picks a certificate-less SRP suite.

## Decision

### 1. The libraries

Two new hand-built libraries in `Cryptography/`, each with its `.UnitTests` twin beside it, held to
the quality gates:

| Library | Holds | References |
| --- | --- | --- |
| `Surl.Cryptography.Srp.UnitLibrary` (BL-564) | SRP-6a for TLS (RFC 5054 sections 2.4 to 2.6, RFC 2945): the seven groups of Appendix A, `PAD`, `x = SHA1(s \| SHA1(I \| ":" \| P))`, the verifier `v = g^x % N`, `k = SHA1(N \| PAD(g))`, `B = k*v + g^b % N`, `u = SHA1(PAD(A) \| PAD(B))`, the `A % N = 0` check, the server's premaster secret `(A * v^u)^b % N`, and the GnuTLS `tpasswd` and `tpasswd.conf` readers of decision 4. `System.Numerics.BigInteger.ModPow`, `SHA1` and `RandomNumberGenerator` do the arithmetic; Appendix B's test vectors are its unit tests. | nothing |
| `Surl.TlsSrp.UnitLibrary` (BL-565, BL-566) | A TLS 1.2 server (RFC 5246) that serves only RFC 5054's suites: the record layer with CBC and HMAC-SHA1 in both MAC-then-encrypt and RFC 7366 encrypt-then-MAC, the PRF with SHA-256, the master secret with and without RFC 7627's extended master secret, the key block, `Finished`, the handshake messages and the server's state machine. It takes a `Stream` and the account lookup and returns a secured `Stream` and what was negotiated, so it knows nothing of the listener seam. | `Surl.Cryptography.Srp.UnitLibrary` |

`Surl.Networking.UnitLibrary` references `Surl.TlsSrp.UnitLibrary` (decision 5). ADR-0002 decision
3's table gains both rows, as written above; `Surl.Protocol.Abstractions.UnitTests` learns each once
its project exists.

**Nothing is shared with `Surl.Tls13` or `Surl.Dtls`.** `Surl.Tls13` is TLS 1.3's HKDF schedule for
QUIC, with nothing in common beyond the hash. `Surl.Dtls` has a TLS 1.2 PRF
(`DtlsPseudorandomFunction`), but its record layer is AEAD-only over datagrams with epochs and
replay windows, and referencing it would pull `Surl.Protocol.Abstractions` and `Surl.Cryptography.
Curve25519` into a library that needs neither. The PRF is RFC 5246 section 5's dozen lines, tested
against the same published vectors, so `Surl.TlsSrp` keeps its own (`TlsPseudorandomFunction`)
rather than couple two record layers that share nothing else.

### 2. The TLS version and the cipher suites

- **TLS 1.2 only** (RFC 5246). TLS 1.3 has no SRP key exchange, and the pinned build offers nothing
  older than 1.2. The ServerHello is `server_version` `03 03` with no `supported_versions`
  extension (RFC 8446 section 4.2.1), and its random carries **no** downgrade sentinel: RFC 8446
  section 4.1.3 asks for one only from a server that supports TLS 1.3, and Surl's TLS-SRP stack
  does not, so a sentinel would make OpenSSL abort. A ClientHello whose `client_version` is below
  `03 03` and that has no `supported_versions` listing `03 03` gets `protocol_version` (70).
- **All nine of RFC 5054 section 2.7's suites** are implemented: 3DES-EDE-CBC (`TripleDES`),
  AES-128-CBC and AES-256-CBC (`Aes`), each with HMAC-SHA1, each unsigned, RSA-signed and
  DSS-signed. Which can be chosen depends on the server's certificate (decision 6):
  - **Neither `--cert` nor `--self-signed`** (the default, and upstream's gnutls-serv): only the
    three unsigned suites `C0 1A`, `C0 1D`, `C0 20`. No certificate is sent, so curl without
    `--insecure` fails with 60, as in case 324. ADR-0032's rule that a secure scheme needs
    `--cert` or `--self-signed` does not apply under TLS-SRP: the verifier, not a certificate, is
    what proves the server, and the password is what proves the client.
  - **`--cert` with an RSA key, or `--self-signed`** (ADR-0010's throwaway RSA certificate): the
    unsigned and the RSA suites. **`--cert` with a DSA key:** the unsigned and the DSS suites. The certificate goes in a `Certificate` message (RFC 5054 section 2.5.2)
    and the ServerKeyExchange carries a TLS 1.2 `digitally-signed` signature over `client_random
    + server_random + ServerSRPParams` (RFC 5054 section 2.8.2, RFC 5246 section 7.4.3), with the
    first scheme in the client's `signature_algorithms` the key can make (RSA: `08 04`
    rsa_pss_rsae_sha256 from the measured list; DSA: `04 02` dsa_sha256).
- **Choice:** the first suite in the client's order that the server can serve. Against the
  measured ClientHello that is `C0 20` (SRP_SHA_AES_256) without a certificate, `C0 21` with an RSA
  one and `C0 22` with a DSA one. The suite's PRF and handshake hash are SHA-256 (RFC 5246 section
  5: RFC 5054 names none). No offered SRP suite the server can serve: `handshake_failure` (40).
- **Key block** (RFC 5246 section 6.3): client and server MAC keys (20 bytes), then client and
  server encryption keys (24, 16 or 32); no IVs, because a TLS 1.2 CBC record carries its own
  explicit IV (section 6.2.3.2), drawn from `RandomNumberGenerator` per record.
- **Session ID:** the ServerHello's is empty, so no session is cached and every connection runs the
  full handshake (RFC 5246 section 7.4.1.3; RFC 5054 section 2.5.1.1's resumption is not offered).

### 3. The extensions

| Extension | What the server does |
| --- | --- |
| `srp` (12) | Reads the user name (RFC 5054 section 2.8.1). A zero-length or malformed one: `decode_error` (50). Absent while the only suites in common are SRP's: `unknown_psk_identity` (115) straight after the ClientHello (section 2.5.1.2). |
| `renegotiation_info` (`ff 01`) and `00 FF` | Answers an empty `renegotiation_info` (RFC 5746 section 3.6); a non-empty one in an initial hello: `handshake_failure` (40). A client with neither is still served (RFC 5746 section 4.3), noted in the verbose log. Renegotiation is never offered: a ClientHello after the handshake gets the warning `no_renegotiation` (100) and the connection carries on (RFC 5246 section 7.2.2). |
| `extended_master_secret` (`00 17`) | Echoed empty; the master secret is then derived from the session hash (RFC 7627 sections 4 and 5.1). Without it, the RFC 5246 section 8.1 master secret. |
| `encrypt_then_mac` (`00 16`) | Echoed empty; every record after `ChangeCipherSpec` is encrypt-then-MAC (RFC 7366 section 3). Without it, MAC-then-encrypt with the padding check in constant time over the whole record (RFC 5246 section 6.2.3.2's note on timing). |
| `application_layer_protocol_negotiation` (`00 10`) | Agrees `http/1.1` when offered, never `h2`: every SRP suite is a CBC suite, which HTTP/2 forbids over TLS 1.2 (RFC 9113 section 9.2.2, `INADEQUATE_SECURITY`). A list without `http/1.1`: `no_application_protocol` (120) (RFC 7301 section 3.2). None offered: served without ALPN. |
| `signature_algorithms` (`00 0d`) | Picks the ServerKeyExchange signature for the RSA and DSS suites (decision 2). Absent: RFC 5246 section 7.4.1.4.1's default, SHA-1 with the key's algorithm. |
| `server_name` (`00 00`) | The host name is kept for `TlsSession.ServerName`; not acknowledged (RFC 6066 section 3 lets the server stay silent). |
| `supported_groups`, `key_share`, `supported_versions` (beyond decision 2's check), `psk_key_exchange_modes`, `post_handshake_auth`, `session_ticket`, `status_request`, `ec_point_formats`, every unknown type | Ignored, as RFC 5246 section 7.4.1.4 requires of an extension the server does not support; none is echoed. The ECC and TLS 1.3 ones name no SRP suite (RFC 8422 section 5.1). |

Two extensions of the same type: `decode_error` (50) (RFC 5246 section 7.4.1.4). A ClientHello
without the `null` compression method: `illegal_parameter` (47).

### 4. Where the verifiers come from

Two sources, which may be combined, both read once at startup before any listener binds:

- **`--tlsuser <name>` and `--tlspassword <password>`:** one account. At startup Surl draws a
  16-byte salt from `RandomNumberGenerator` and computes the verifier with RFC 5054 Appendix A's
  **2048-bit group** (generator 2): larger than the 1024 bits OpenSSL's client insists on, and the
  largest Appendix A group with generator 2, as upstream's own `srp-verifier-conf` groups all have.
  The password lives only as long as the verifier computation.
- **`--srp-verifiers <file>` and `--srp-groups <file>`**, always together: GnuTLS's `tpasswd` file
  (`user:verifier:salt:index`, one line per user) and `tpasswd.conf` file (`index:N:g`), the
  formats gnutls-serv reads and upstream's `tests/certs/srp-verifier-db` and `srp-verifier-conf`
  are written in. Their numbers and salts use GnuTLS's SRP base64 (`lib/auth/srp_sb64.c`: the
  alphabet `0-9A-Za-z./`, left-padded to a multiple of three bytes), not RFC 4648's. Any group is
  accepted; one that is not in Appendix A is noted once in the verbose log, since OpenSSL's client
  will refuse it (Context).

A user name is compared as the exact bytes the `srp` extension carries, and the password's verifier
is computed from its UTF-8 bytes: RFC 5054 section 2.3 asks for SASLprep, but OpenSSL's client
applies none, and matching what the client actually hashes is what makes a login succeed. A user in
both sources, or twice in one, is `FailedInit` (2).

**An unknown user** is answered as RFC 5054 section 2.5.1.3 suggests and gnutls-serv does: the
handshake goes on with a simulated entry, so the client cannot tell a wrong user from a wrong
password. The simulated salt is the first 16 bytes of HMAC-SHA256 over the UTF-8 user name keyed
with a 32-byte secret drawn once per process, so a name gets the same salt on every attempt; the
group is the 2048-bit one; the verifier is random. The client's `Finished` then fails to verify and
the server sends `bad_record_mac` (20), exactly as for a wrong password (RFC 5054 section 2.6). The
verbose log says which it was; the wire never does.

### 5. The listener seam

`Surl.Protocol.Http` and every other protocol server run over TLS-SRP unchanged, because they see
only `IConnection`, which always carries plaintext (ADR-0004 section 2, ADR-0010 section 1):

- `Surl.Networking.UnitLibrary` gains a TLS-SRP handshake beside `ServerTlsHandshake`. When the
  process's TLS settings say SRP, a listener's handshake runs `Surl.TlsSrp`'s server over the
  connection's transport stream instead of `SslStream`, and the connection then reads and writes
  through the stream it returns, as it does through an `SslStream` today. Implicit-TLS listen URLs
  and the `STARTTLS`-style upgrades through `IConnection.UpgradeToTlsAsync` both get it, so
  `--tlsauthtype SRP` covers `https`, `wss`, `ftps`, `imaps`, `pop3s`, `smtps` and the HTTPS proxy
  of ADR-0088 alike, as curl's `--tlsauthtype` and `--proxy-tlsauthtype` reach any TLS
  connection. Only `https` has upstream cases.
- `TlsSession` is filled from what `Surl.TlsSrp` negotiated: `Protocol` `SslProtocols.Tls12`,
  `CipherSuite` the chosen one (`TlsCipherSuite` has all nine), `ApplicationProtocol`
  `http/1.1` or `null`, `ServerName` from SNI, `ClientCertificate` `null`. It gains one member,
  **`SrpUserName`** (`string?`, the authenticated SRP user, `null` on any other session), so a
  server can log who logged in and decision 7's server half can echo it. A simulated (unknown)
  user never completes a handshake, so it never appears there.
- `Surl.TlsSrp` constructs no `Socket`, `TcpListener`, `UdpClient`, `SslStream` or
  `HttpListener`: it works on the `Stream` it is handed. `Surl.Networking` stays the only library
  that constructs those (root `CLAUDE.md`).
- A failed TLS-SRP handshake is a `TlsHandshakeException` on that connection, as an `SslStream`
  failure is: the connection is closed and logged, and the process keeps serving.

### 6. The command line, exit codes and alerts

```
surl --tlsauthtype SRP --tlsuser jsmith --tlspassword abc https://127.0.0.1:8443
surl --tlsuser jsmith --tlspassword abc https://127.0.0.1:8443
surl --tlsauthtype SRP --srp-verifiers srp-verifier-db --srp-groups srp-verifier-conf https://127.0.0.1:8443
```

| Option | Argument | Meaning in surl |
| --- | --- | --- |
| `--tlsauthtype` | `<SRP>` | Every TLS listener serves TLS-SRP (decision 5) instead of `SslStream`. Case-insensitive, curl's one word. Implied by `--tlsuser` with `--tlspassword`, as in curl. |
| `--tlsuser` | `<name>` | The one account's user name (decision 4). Needs `--tlspassword`. |
| `--tlspassword` | `<password>` | The one account's password. Needs `--tlsuser`. |
| `--srp-verifiers` | `<file>` | A GnuTLS `tpasswd` file of accounts. Needs `--srp-groups`. |
| `--srp-groups` | `<file>` | The GnuTLS `tpasswd.conf` file its index column names. Needs `--srp-verifiers`. |

`--cert`, `--cert-type`, `--key`, `--key-type` and `--pass` keep ADR-0010's meaning and add the
signed suites (decision 2), and so does `--self-signed`; with neither, no certificate at all is
served, and ADR-0032's need for one is lifted.

| Failure at startup | `SurlExitCode` |
| --- | --- |
| `--tlsauthtype` with a word other than `SRP` | `FailedInit` (2), `option --tlsauthtype: is badly used here` (ADR-0007, row 36) |
| `--tlsuser` without `--tlspassword` or the reverse; `--srp-verifiers` without `--srp-groups` or the reverse; `--tlsauthtype SRP` with no account from either source; a user given twice | `FailedInit` (2) |
| Any TLS-SRP option and no listen URL that uses TLS | `FailedInit` (2) |
| TLS-SRP beside `--http3` (QUIC is TLS 1.3, which has no SRP), `--cacert` (the client proves itself with its password, so no client certificate is requested), or `--tls-max` below 1.2 | `FailedInit` (2) |
| `--srp-verifiers` or `--srp-groups` missing, unreadable, or with a malformed line, a bad base64 number or a group index the groups file lacks | `CouldNotReadFile` (37), naming the file and line (as ADR-0092 does for a zone file) |
| `--cert` whose key is neither RSA nor DSA under TLS-SRP (no SRP suite can sign with it), and every ADR-0010 certificate failure | `CertificateProblem` (58) |

No new `SurlExitCode` member. BL-567 pins the stderr texts and writes each option's `--aihelp`
facts, its `HelpCategories` rows and an `AiHelpExamples` entry.

**Alerts**, all fatal unless noted (RFC 5246 section 7.2, RFC 5054 section 2.9):

| Failure during the handshake | Alert |
| --- | --- |
| No SRP suite in common that the server can serve | `handshake_failure` (40) |
| Neither `client_version` nor `supported_versions` reaches TLS 1.2 | `protocol_version` (70) |
| SRP suites only, no `srp` extension | `unknown_psk_identity` (115) |
| Malformed message, bad length, duplicate extension, empty user name, `A` longer than `N` | `decode_error` (50) |
| `A % N = 0` (RFC 5054 section 2.5.4); no `null` compression | `illegal_parameter` (47) |
| Wrong password or unknown user, seen as the client's `Finished` failing its MAC (RFC 5054 sections 2.5.1.3 and 2.6); any record whose MAC or padding fails | `bad_record_mac` (20) |
| `Finished` decrypts but its `verify_data` differs | `decrypt_error` (51) |
| A message out of order | `unexpected_message` (10) |
| A record longer than RFC 5246 section 6.2.3 allows | `record_overflow` (22) |
| ALPN list without `http/1.1` | `no_application_protocol` (120) |
| ClientHello after the handshake | `no_renegotiation` (100), warning |

A fatal alert from the client ends the connection with no reply.

### 7. How the upstream cases' server half plays `httptls+srp` and `httptls+srp-ipv6`

- **Data.** BL-568 extends `Import-UpstreamTestCases.ps1` to copy `tests/certs/srp-verifier-db` and
  `tests/certs/srp-verifier-conf` byte for byte with the rest, in `SHA256SUMS`, and the server half
  reads them through decision 4's `--srp-verifiers` and `--srp-groups` readers, so the verifiers
  and salts are upstream's own and no password is written into Surl. That `jsmith`/`abc` passes
  and `jsmith`/`badpass` fails against upstream's file is itself a check of BL-564's reader.
- **The server.** `httptls+srp` is BL-391's HTTP server half over `Surl.TlsSrp` on
  `%HTTPTLSPORT` on `%HOSTIP`, with no certificate (decision 2), like gnutls-serv in
  `runhttptlsserver`; `httptls+srp-ipv6` is the same on `%HOST6IP`. Both server-name rows flip to
  built together.
- **The reply.** gnutls-serv `--http` writes its own page, not the case's `<reply>` (320's
  `<data>` is `nocheck`), so for these two servers the server half composes the reply from the
  request and the session in gnutls-serv's shape (GnuTLS `src/serv.c`, `peer_print_info`):
  `HTTP/1.0 200 OK\r\n`, `Content-type: text/html\r\n\r\n`, then an HTML page whose lines start
  with `<` except the request's header fields, echoed raw after the request line inside
  `<hr><P>Your HTTP header was:<PRE>` and ending `</PRE></P>`, with a line
  `<p>Connected as user '<TlsSession.SrpUserName>'.</p>`; then the connection closes. After
  320's `<stripfile>` (which turns the user line into `FINE` and drops every line starting with
  `<`, which takes the `Host` line with the `<PRE>` before it) that is exactly 320's verify file.
  The request is still read by Surl's real HTTP reader.
- **The outcomes Surl must reproduce:** 320 completes with that page; 321 (`baduser`) and 322
  (`jsmith`/`badpass`) end in `bad_record_mac`, which curl reports as 35; 324 completes the
  handshake with no certificate and curl itself refuses it with 60.
- **Where they run:** the Linux and macOS reference pins, in CI (BL-392's job); on Windows each of
  the four skips `feature-missing:TLS-SRP`.

## Consequences

Good:

- curl's `--tlsauthtype SRP` has a server that answers it on every TLS scheme Surl serves, built
  only on the BCL, and the four upstream cases can run.
- The SRP math and the record layer are each testable alone: Appendix B's vectors for one, RFC 5246
  PRF vectors and recorded curl bytes for the other.
- Upstream's verifier file is served as is, so the cases test Surl against upstream's own
  credentials.

Bad:

- A second TLS 1.2 PRF beside `Surl.Dtls`'s (decision 1). It is small and vector-tested.
- CBC with HMAC-SHA1 and 3DES are weak by today's standards; they are what RFC 5054 defines and
  what curl offers, and Surl serves them only when asked for TLS-SRP.
- The cases cannot run on a lane, which tests on Windows only; their results come from CI.

## Alternatives considered

- **Serve TLS-SRP only for `https`.** Rejected: the seam already makes every TLS listener the same,
  and curl's `--tlsauthtype` reaches every TLS scheme; limiting it would be extra code.
- **Mirror gnutls-serv's option names (`--srppasswd`, `--srppasswdconf`).** Rejected: Surl's names
  say what the file holds (root `CLAUDE.md`, "Say what it does"); the file formats are gnutls-serv's.
- **Answer an unknown user with `unknown_psk_identity` at once.** Rejected: it tells a client which
  user names exist; RFC 5054 section 2.5.1.3 and gnutls-serv both simulate, and curl's exit (35)
  is the same either way.
- **Serve the throwaway certificate and the RSA suites by default.** Rejected: upstream's server
  sends no certificate, and case 324's 60 is curl refusing exactly that.
- **Share `Surl.Dtls`'s PRF and record code.** Rejected in decision 1.

## Amendment 1 (2026-11-05): what BL-566 measured, and the signed suites deferred to BL-911

Decided by Claude under Stewart's delegation, in BL-566.

**Measured.** BL-566 ran the supplementary static Windows build (OpenSSL 4.0.1, SHA-256
`589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`) against `Surl.TlsSrp`'s own
server, using `Record-CurlExchange.ps1 -TlsSrp` and `Run-TlsSrpRelay.cs`. Its ClientHello is the one
described in Context, and the confirmations below hold. The recorded bytes are in
`Cryptography/Surl.TlsSrp.UnitTests/Fixtures`.

- With `-k --tlsauthtype SRP --tlsuser jsmith --tlspassword abc`, the handshake completes:
  `C0 20`, with `renegotiation_info`, `extended_master_secret`, `encrypt_then_mac` and ALPN
  `http/1.1` answered, as decisions 2 and 3 predicted. The request and the reply then round-trip
  (exit 0). This is upstream case 320's outcome.
- `jsmith`/`badpass` and `baduser`/`badpass` both end with the server's `bad_record_mac` at the
  client's `Finished`. curl reports `tls alert bad record mac` and exits 35, as in cases 322 and
  321, and the wire does not tell the two apart (decision 4).
- Without `-k`, the handshake completes and curl exits 60, `SSL: could not get peer certificate`,
  as in case 324.

**A salt must not start with a zero byte.** With the salt `00 01 ... 0F`, the right password
failed with `bad_record_mac`; with `10 11 ... 1F`, it passed. OpenSSL's client reads `srp_s` into
a number, so it hashes the salt without its leading zero bytes, while the verifier was computed
over all 16. The server sends the salt as it has it. So decision 4's salt drawn at startup for
`--tlsuser` and `--tlspassword` (BL-567) is drawn again until its first byte is not zero. A
verifier file whose salt starts with zero cannot be fixed by the server: it fails against OpenSSL
exactly as it would against gnutls-serv.

**Signed suites.** BL-566 serves the three unsigned suites only, which is all that upstream's
gnutls-serv and the four `httptls+srp` cases use. Decision 2's RSA- and DSS-signed suites, with
their `Certificate` message and signed ServerKeyExchange, are BL-911's work.

**Client alerts.** During the handshake, any alert from the client ends it with no reply, the
warning `close_notify` included. After the handshake, a warning other than `close_notify` is
ignored, and `close_notify` or the end of the connection ends the stream.

**The returned stream** (`TlsSrpStream`) is asynchronous only. Its synchronous `Read` and `Write`
throw `NotSupportedException`, and `DisposeAsync` sends `close_notify` before it closes the
connection. BL-567's seam in `Surl.Networking` reads and writes it asynchronously.
