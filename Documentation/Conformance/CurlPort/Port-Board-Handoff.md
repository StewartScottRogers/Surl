# Phase 7 handoff to the Curl port's board

**How this file is filed.** No Surl session writes into the Curl port's repository
(`Z:\repos\Curl`). Stewart files these findings himself: in a session of the port's repository he runs
the port's `/task-plan` on this file, so he sees each finding before it becomes the port's work. The
port's planner allocates each task's `id` and chooses its `touches`; both are left blank below
([ADR-0089](../../Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md)
decision 7).

**Surl does not change to make the port pass.** Upstream curl is the only oracle. Wherever the port and
pinned upstream curl disagree against the same `surl`, upstream is right, the disagreement is the
port's defect, and it is filed on the port's own board (ADR-0003 decision 6). Every finding below names
the upstream documentation or specification that says what is right, so the port's maintainer can fix
it without re-deriving it.

**What this file holds.** One entry per finding in the Phase 7 findings files
([summary](README.md)): 18 entries, the 14 of the family files, the 3 of `Upstream-Test-Cases.md` and
the 1 of [`Extended.md`](Extended.md), the extended clients. Each entry is in the port's task shape (its `.claude/skills/task-board/TASK-TEMPLATE.md`): front matter fields, then `Goal`,
`Context` and `Acceptance criteria`.

**Evidence common to every entry.** Measured on Windows (`win-x64`), 2026-11-05 and 2026-11-06 (entry 18: 2026-11-11, with no
upstream build, since upstream curl has no `webtransport` scheme).

| Item | Value |
| --- | --- |
| Port commit | `b42c319ba940f66452b2b6f3e9910d46825b08fe` |
| Port `curl.exe` SHA-256 | `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel`) |
| Port `Libcurl.dll` SHA-256 | `613976A7CC3DA746AB74819C6A526D676A39998D56B5470D5C527A902D7C8311` (entry 14 only) |
| Upstream reference build | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` |
| Upstream static OpenSSL build | SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0` |
| Upstream `libcurl-4.dll` | SHA-256 `799F7EEFC3C9DA9C80EC5AEA221A02B3AFE2C5350C6B45FD5A4865E7E2D4E574`, `libcurl/8.21.0 Schannel …` (entry 14 only) |
| Upstream documentation | curl 8.21.0: https://curl.se/docs/manpage.html, https://curl.se/libcurl/c/libcurl-errors.html, and the source at tag `curl-8_21_0` |

Each entry names its own Surl commit, Surl test and the findings file that holds the full record.

| # | Title | Family | Cases |
| ---: | --- | --- | ---: |
| 1 | Offer ssh-ed25519 as a host key algorithm on an OpenSSL-backed build | HTTP/1.1 and HTTPS | 1 |
| 2 | Disconnect an SFTP session before closing its channel, as libssh2 does | HTTP/1.1 and HTTPS | 1 |
| 3 | Log in with SASL GSSAPI over SMTP, IMAP and POP3 | HTTP/1.1 and HTTPS | 6 |
| 4 | Fall back to NTLM inside SPNEGO for --negotiate, as curl 8.21.0 as tagged does | HTTP/1.1 and HTTPS | 2 |
| 5 | Offer the OpenSSL backend's SSH algorithms on an OpenSSL-backed build | SCP and SFTP | 5 |
| 6 | Open and leave idle curl's own connection before WinLDAP's on ldap:// | LDAP | 12 |
| 7 | Bind with NTLM, Negotiate or Digest over LDAP when --ntlm, --negotiate or --digest is given | LDAP | 6 |
| 8 | Leave the ldaps certificate check to WinLDAP under -k | LDAP | 1 |
| 9 | Turn the smb and smbs protocols on | SMB | 24 |
| 10 | Answer an RTSP Digest challenge on the same connection | RTSP | 3 |
| 11 | Offer h2 by ALPN on https by default | HTTP/2 | 9 |
| 12 | Support --http3 and --http3-only | HTTP/3 | 12 |
| 13 | Negotiate HTTP/2 over https when the server also advertises h3 | HTTP/3 | 1 |
| 14 | Keep an RTSP session and read interleaved data on its connection in CURL_RTSPREQ_RECEIVE | libcurl | 1 |
| 15 | Save a cookie whose Domain equals the host as a domain cookie | Upstream test cases | 2 |
| 16 | Send an empty SASL EXTERNAL response as = | Upstream test cases | 3 |
| 17 | Write --libcurl's setopt calls in upstream's order | Upstream test cases | 6 |
| 18 | Fail the webtransport transfer when the server refuses the CONNECT | Extended clients | 1 |

---

## 1. Offer ssh-ed25519 as a host key algorithm on an OpenSSL-backed build

- **title:** Offer ssh-ed25519 as a host key algorithm on an OpenSSL-backed build
- **priority:** Normal · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

The port's SSH client lists `ssh-ed25519` in its `KEXINIT` host key algorithms, so against a server
that holds only an Ed25519 host key it agrees the algorithm and then judges the key, as upstream curl
8.21.0 with libssh2 1.11.1 on OpenSSL does.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http.md`, finding 1. Related: entry 5 (the same
  backend algorithm list, seen from the SSH family).
- **Case:** `CurlPortBesideUpstreamCurlOverHttpTests.Download_Ed25519HostCertificateOnTheOpenSslWindowsBuild_AgreesThePlainKeyAndExits60`,
  mirroring `UpstreamCurlTrustsSurlsHostCertificateTests` of the same name. Surl commit
  `489f8d6f9b80614937f188b1a41896b8c5ed0e5c`.
- **Command line:** `curl -sS --knownhosts <known_hosts> -u tester:secret sftp://127.0.0.1:<port>/a.txt`
  against `surl -v --hostkey <Ed25519 key> --hostcert <its certificate> sftp://127.0.0.1:0/`;
  `known_hosts` holds only an `@cert-authority [127.0.0.1]:<port>` line.
- **Upstream build:** static OpenSSL build, SHA-256 `589C8E4D…2648`.
- **Upstream result:** exit 60, `curl: (60) SSL peer certificate or SSH remote key was not OK`; `surl`
  notes `SSH negotiated kex curve25519-sha256, host key ssh-ed25519, …`.
- **Port result:** exit 2, `curl: (2) Failure establishing ssh session: -5, Unable to exchange encryption keys`;
  `surl` notes `SSH no common host key algorithm; client offered rsa-sha2-512,rsa-sha2-256,rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,ssh-rsa,ssh-rsa-cert-v01@openssh.com`.
- **What decides:** RFC 8709 section 4 (`ssh-ed25519`), RFC 4253 section 7.1 (the client lists every
  host key algorithm it supports); libssh2 1.11.1 `src/hostkey.c` compiles `ssh-ed25519` in on OpenSSL.

### Acceptance criteria

- [ ] The port's SSH client offers `ssh-ed25519` among its host key algorithms on the build it pairs
      with upstream's OpenSSL build, shown by a port unit test of its `KEXINIT` name-list.
- [ ] Against a server holding only an Ed25519 host key not in `known_hosts`, the port exits 60
      (`CURLE_PEER_FAILED_VERIFICATION`), shown by a port test.

## 2. Disconnect an SFTP session before closing its channel, as libssh2 does

- **title:** Disconnect an SFTP session before closing its channel, as libssh2 does
- **priority:** Low · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

When an SFTP transfer ends, the port sends `SSH_MSG_DISCONNECT` with the SFTP channel still open, as
upstream curl 8.21.0 with libssh2 1.11.1 does, instead of closing the channel and waiting for its exit
status first.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http.md`, finding 2.
- **Case:** `CurlPortBesideUpstreamCurlOverHttpTests.Download_RsaHostCertificateWithThePlainKeyPinned_DownloadsTheFile`.
  Surl commit `489f8d6f9b80614937f188b1a41896b8c5ed0e5c`.
- **Command line:** `curl -sS --hostpubsha256 <surl's ssh-rsa key SHA-256> -u tester:secret sftp://127.0.0.1:<port>/a.txt`
  against `surl -v --hostkey <RSA key> --hostcert <its certificate> sftp://127.0.0.1:0/`.
- **Upstream build:** reference build, SHA-256 `0E773709…8778`.
- **Upstream result:** exit 0, the file's 12 bytes; after `SFTP CLOSE /a.txt: read 12 bytes`, `surl`
  notes `SSH disconnect received: 11 Shutdown`, then `SFTP session ended: client EOF`, and no channel end.
- **Port result:** exit 0, the same bytes; `surl` notes `SFTP session ended: client EOF`,
  `SSH channel 0 ended: exit status 0`, then `SSH disconnect received: 11 Shutdown` (same in three runs).
- **What decides:** RFC 4254 section 5.3 and RFC 4253 section 11.1 allow both orders; ADR-0089 makes
  upstream curl 8.21.0's order the expected one, since a server that watches the shutdown sees the two
  clients differently. The port's maintainer may record that the port keeps its order instead.

### Acceptance criteria

- [ ] After an SFTP download the port sends `SSH_MSG_DISCONNECT` before any `SSH_MSG_CHANNEL_CLOSE` for
      the SFTP channel, shown by a port test of the message order; or a port ADR records why it keeps
      its order.

## 3. Log in with SASL GSSAPI over SMTP, IMAP and POP3

- **title:** Log in with SASL GSSAPI over SMTP, IMAP and POP3
- **priority:** Normal · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

With `--login-options AUTH=GSSAPI` and `-u user@REALM:password`, the port completes a SASL `GSSAPI`
(Kerberos) login over SMTP, IMAP and POP3 on Windows, as upstream curl 8.21.0 does through SSPI.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http.md`, finding 3.
- **Cases (6):** `CurlPortBesideUpstreamCurlOverHttpTests.GssapiOverSmtp_Account_StoresTheMessageInTheAccountsInbox`,
  `GssapiOverImap_Account_FetchesTheDeliveredMessage` and `GssapiOverPop3_Account_RetrievesTheDeliveredMessage`,
  each with and without `--sasl-ir`. Surl commit `489f8d6f9b80614937f188b1a41896b8c5ed0e5c`.
- **Command lines**, against `surl --auth gssapi --keytab <keytab> --user-file <tester@SURL.TEST>` and a
  test KDC on `127.0.0.1:88`, with `--resolve mail.surl.test:<port>:127.0.0.1`:
  - `curl -sS -u tester@SURL.TEST:surl-test-password --login-options AUTH=GSSAPI --mail-from a@x --mail-rcpt "tester@SURL.TEST"@example.com -T mail.txt smtp://mail.surl.test:<port>/c`
  - `curl -sS -u tester@SURL.TEST:surl-test-password --login-options AUTH=GSSAPI imap://mail.surl.test:<port>/INBOX;UID=1`
  - `curl -sS -u tester@SURL.TEST:surl-test-password --login-options AUTH=GSSAPI pop3://mail.surl.test:<port>/1`
- **Upstream build:** reference build, SHA-256 `0E773709…8778`.
- **Upstream result:** exit 0 in all six; SMTP stores the message, IMAP and POP3 write it
  (first line `Return-Path: <a@x>`).
- **Port result:** exit 67, `curl: (67) Login denied`, in all six; nothing stored or written.
- **What decides:** RFC 4752 (`GSSAPI` SASL mechanism), carried by RFC 4954 (SMTP), RFC 9051 section
  6.2.2 (IMAP) and RFC 5034 (POP3); curl 8.21.0's `--login-options` (manual) selects it.

### Acceptance criteria

- [ ] The port completes a SASL `GSSAPI` login over SMTP, IMAP and POP3, with and without `--sasl-ir`,
      shown by port tests of each exchange.
- [ ] `--login-options AUTH=GSSAPI` with a valid Kerberos principal no longer exits 67
      (`CURLE_LOGIN_DENIED`) where the server accepts the ticket.

## 4. Fall back to NTLM inside SPNEGO for --negotiate, as curl 8.21.0 as tagged does

- **title:** Fall back to NTLM inside SPNEGO for --negotiate, as curl 8.21.0 as tagged does
- **priority:** Low · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

With `--negotiate` against a host with no Kerberos realm, the port's SPNEGO offers NTLMSSP and logs in,
as upstream curl 8.21.0 as tagged does; or the port records that it follows upstream's later `!ntlm`
change instead.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http.md`, finding 4.
- **Cases (2):** `CurlPortBesideUpstreamCurlOverHttpTests.NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes`
  and `NegotiateOverHttp_WrongPassword_IsRefusedWithHttpReturnedError`. Surl commit
  `489f8d6f9b80614937f188b1a41896b8c5ed0e5c`.
- **Command lines:** `curl -sS --negotiate -u tester:secret http://127.0.0.1:<port>/hello.txt` and
  `curl -sS -f --negotiate -u tester:wrong http://127.0.0.1:<port>/hello.txt`, against
  `surl --auth negotiate --user-file <tester:secret> http://127.0.0.1:0/`.
- **Upstream build:** static OpenSSL build, SHA-256 `589C8E4D…2648` (8.21.0 as tagged, which still
  offers NTLM inside SPNEGO).
- **Upstream result:** right password, exit 0, `hello from surl`; wrong password, exit 22,
  `curl: (22) The requested URL returned error: 401`.
- **Port result:** right password, exit 0 with nothing written (it took the 401 as the answer); wrong
  password, exit 22,
  `curl: (22) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`.
- **What decides:** RFC 4559 section 4 and RFC 4178 (SPNEGO may negotiate NTLMSSP). The port behaves as
  the Windows reference build does, which carries upstream's later `!ntlm` change; measured against
  8.21.0 as tagged, it differs. Which upstream behaviour the port follows is its maintainer's call.

### Acceptance criteria

- [ ] With `--negotiate -u tester:secret` against a server offering `Negotiate` and no Kerberos realm,
      the port completes an NTLMSSP-in-SPNEGO login and writes the body, shown by a port test; or a port
      ADR records that the port follows the `!ntlm` behaviour, naming the upstream change.

## 5. Offer the OpenSSL backend's SSH algorithms on an OpenSSL-backed build

- **title:** Offer the OpenSSL backend's SSH algorithms on an OpenSSL-backed build
- **priority:** Normal · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

The port's SSH client offers the key exchange, host key, cipher and MAC lists libssh2 1.11.1 offers on
OpenSSL (Curve25519, ECDH, ECDSA, Ed25519, AES-GCM, and on request Blowfish, CAST-128 and RIPEMD-160),
where it now offers only the WinCNG backend's lists.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Ssh.md`, SSH-1. Related: entry 1.
- **Cases (5):** `CurlPortBesideUpstreamCurlOverSshTests.KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms`,
  `Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile[blowfish-cbc]` and `[cast128-cbc]`,
  `Download_MacOnlySurlOffers_AgreesItAndWritesTheFile[hmac-ripemd160]` and
  `[hmac-ripemd160@openssh.com]`. Surl commit `4c5010959a6096d5ee8bfcdfbef890e9d6160653`.
- **Command lines:** `curl -sS -k sftp://127.0.0.1:<port>/x` against a `KEXINIT` recorder; and
  `curl -sS --hostpubsha256 <surl's ssh-rsa SHA-256> -u tester:secret sftp://127.0.0.1:<port>/a.txt`
  against `surl sftp://… --throwaway-hostkey --user tester:secret --allow-weak-ssh-algorithms` with
  `--ssh-ciphers blowfish-cbc`, `--ssh-ciphers cast128-cbc` (curl run with `OPENSSL_CONF` activating the
  `legacy` provider), or `--ssh-ciphers aes128-ctr --ssh-macs hmac-ripemd160` (or `…@openssh.com`).
- **Upstream build:** static OpenSSL build, SHA-256 `589C8E4D…2648`.
- **Upstream result:** `KEXINIT` kex list starts
  `curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,…`; host keys include ECDSA and
  `ssh-ed25519`; ciphers include `aes256-gcm@openssh.com`, `blowfish-cbc`, `cast128-cbc`; MACs include
  `hmac-ripemd160`. Each weak-algorithm download exits 0 with `hello world\n`.
- **Port result:** `KEXINIT` differs from line 2: exactly the WinCNG lists (RSA host keys only; no GCM,
  Blowfish, CAST-128 or RIPEMD-160). Each download exits 2,
  `curl: (2) Failure establishing ssh session: -5, Unable to exchange encryption keys`. (Against the
  reference build, `KexInit_WindowsBuild_ListsTheWinCngAlgorithms` agrees.)
- **What decides:** libssh2 1.11.1 `src/crypt.c`, `src/mac.c`, `src/kex.c`, `src/hostkey.c` (OpenSSL
  entries under `LIBSSH2_OPENSSL`); RFC 4253 section 6.3, RFC 5656, RFC 8731, RFC 8709. Whether the port
  follows the OpenSSL builds or keeps the reference build's lists is its maintainer's call.

### Acceptance criteria

- [ ] The port's `KEXINIT` on the build it pairs with upstream's OpenSSL build lists the OpenSSL
      backend's name-lists in libssh2 1.11.1's order, shown by a port test; or a port ADR records that
      the port keeps the WinCNG lists.
- [ ] Against a server offering only `blowfish-cbc`, `cast128-cbc`, `hmac-ripemd160` or
      `hmac-ripemd160@openssh.com`, the port completes the SFTP download (exit 0), shown by port tests.

## 6. Open and leave idle curl's own connection before WinLDAP's on ldap://

- **title:** Open and leave idle curl's own connection before WinLDAP's on ldap://
- **priority:** Low · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

On an `ldap://` URL the port opens curl's own connection to the host and leaves it idle while the LDAP
exchange runs on a second connection, as upstream curl 8.21.0's `WinLDAP` backend does; or the port
records that it keeps one connection.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Ldap.md`, F1.
- **Cases (12):** every `CurlPortBesideUpstreamCurlOverLdapTests.WinLdap_` case with a simple or
  anonymous bind, e.g. `WinLdap_Search_BaseWithSimpleBind_Exits0WithTheBaseEntry` (all listed in
  `Ldap.md`). Surl commit `00351846bd295957d066acd7ac6a9faa68c78e84`.
- **Command line, e.g.:** `curl -sS -m 20 -u alice:secret ldap://127.0.0.1:<port>/dc=example,dc=com`.
- **Upstream build:** reference build, SHA-256 `0E773709…8778`.
- **Upstream result:** two TCP connections; connection 0 carries 0 bytes, connection 1 the bind, search
  and unbind (112 bytes from `30 84 00 00 00 1B 02 01 01 60 84 …`). `surl` notes two exchanges.
- **Port result:** one connection carrying exactly upstream's connection 1 bytes. First differing request
  byte: connection 0, offset 0 (`0x30` from the port; upstream's connection ends there). Exit codes,
  output and standard error agree.
- **What decides:** curl 8.21.0 `lib/ldap.c`: `ldap_do()` passes the host and port to `ldap_init()` /
  `ldap_sslinit()`, so `WinLDAP` opens its own connection and curl's stays idle. Upstream is right by
  ADR-0003 decision 6; the port's maintainer decides whether to reproduce it.

### Acceptance criteria

- [ ] On `ldap://` the port opens curl's own connection, sends nothing on it, and runs the LDAP exchange
      on a second connection, shown by a port test counting connections; or a port ADR records that the
      port keeps one connection.

## 7. Bind with NTLM, Negotiate or Digest over LDAP when --ntlm, --negotiate or --digest is given

- **title:** Bind with NTLM, Negotiate or Digest over LDAP when --ntlm, --negotiate or --digest is given
- **priority:** High · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

With `-u` and `--ntlm`, `--negotiate` or `--digest` on an `ldap://` URL, the port binds through SSPI with
that method, as upstream curl 8.21.0 does, and never sends the password in a simple bind.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Ldap.md`, F2.
- **Cases (6):** `CurlPortBesideUpstreamCurlOverLdapTests.WinLdap_Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer`
  `[ntlm, --ntlm]`, `[negotiate, --negotiate]` and `[digest-md5, --digest]`,
  `WinLdap_Bind_NtlmWrongPassword_Exits38InvalidCredentials`,
  `WinLdap_Bind_NtlmNotAccepted_Exits38AuthenticationMethodNotSupported`,
  `WinLdapKerberos_NegotiateBind_KerberosAccount_Exits0WithTheEntryThroughTheSealedLayer`. Surl commit
  `00351846bd295957d066acd7ac6a9faa68c78e84`.
- **Command line, e.g.:** `curl -sS -m 20 --ntlm -u alice:secret ldap://127.0.0.1:<port>/dc=example,dc=com`
  against `surl --user alice:secret --auth ntlm`.
- **Upstream build:** reference build, SHA-256 `0E773709…8778`.
- **Upstream result:** binds with the chosen method, exits 0 with `DN: dc=example,dc=com` (Kerberos:
  `DN: cn=alice,dc=example,dc=com`); wrong password exits 38 `… Invalid Credentials`; method not accepted
  exits 38 `… Authentication Method Not Supported`.
- **Port result:** a simple bind with the plain-text password in every case, refused by `surl`; exit 38,
  `curl: (38) LDAP local: bind via ldap_win_bind Confidentiality Required`, in all six.
- **What decides:** curl 8.21.0 `lib/ldap.c`, `ldap_win_bind_auth()` (`LDAP_AUTH_NTLM`,
  `LDAP_AUTH_NEGOTIATE`, `LDAP_AUTH_DIGEST` for `CURLAUTH_NTLM`, `CURLAUTH_NEGOTIATE`, `CURLAUTH_DIGEST`);
  the manual's `--ntlm`, `--negotiate` and `--digest`. Sending the password in a simple bind where the
  user asked for a challenge-response method is also a credential exposure.

### Acceptance criteria

- [ ] With `--ntlm`, `--negotiate` or `--digest` and `-u` on `ldap://`, the port binds with that method
      and never sends a simple bind carrying the password, shown by port tests of the bind request.
- [ ] A wrong password exits 38 with `… Invalid Credentials`, and a method the server does not accept
      exits 38 with `… Authentication Method Not Supported`, shown by port tests.

## 8. Leave the ldaps certificate check to WinLDAP under -k

- **title:** Leave the ldaps certificate check to WinLDAP under -k
- **priority:** Normal · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

On Windows, `-k` does not make an `ldaps://` connection to a server with an untrusted certificate
succeed: the port exits 38 with `Server Down`, as upstream curl 8.21.0's `WinLDAP` backend does.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Ldap.md`, F3.
- **Case:** `CurlPortBesideUpstreamCurlOverLdapTests.WinLdap_Ldaps_Insecure_Exits38ServerDownBecauseWinLdapChecksTheCertificateItself`.
  Surl commit `00351846bd295957d066acd7ac6a9faa68c78e84`.
- **Command line:** `curl -sS -m 20 -k -u alice:secret ldaps://127.0.0.1:<port>/dc=example,dc=com`
  against `surl --user alice:secret --self-signed`.
- **Upstream build:** reference build, SHA-256 `0E773709…8778`.
- **Upstream result:** exit 38, nothing written,
  `curl: (38) LDAP local: bind via ldap_win_bind Server Down`.
- **Port result:** exit 0, writes `DN: dc=example,dc=com`.
- **What decides:** curl 8.21.0 `lib/ldap.c` hands `ldaps` to `WinLDAP`'s own TLS (`ldap_sslinit()`,
  `LDAP_OPT_SSL`); `-k` turns off curl's own verification but is not passed to `WinLDAP`, which checks
  the certificate against the Windows store and fails with `LDAP_SERVER_DOWN`.

### Acceptance criteria

- [ ] On Windows, `-k` with `ldaps://` to a self-signed server exits 38 with
      `curl: (38) LDAP local: bind via ldap_win_bind Server Down` and writes nothing, shown by a port test.

## 9. Turn the smb and smbs protocols on

- **title:** Turn the smb and smbs protocols on
- **priority:** High · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

The port answers `smb://` and `smbs://` URLs through its own `Curl.Protocol.Smb.UnitLibrary`, as upstream
curl 8.21.0 built with SMB does, instead of exiting 1 with `Protocol "smb" is disabled`, and `curl -V`
lists `smb` and `smbs`.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Smb.md` (one cause, 24 rows).
- **Cases (24):** every `CurlPortBesideUpstreamCurlOverSmbTests` case, e.g.
  `Download_ServedFile_WritesItsBytes[files/hello.txt]`, `Upload_UploadsOn_StoresTheBytes[up.txt, files/up.txt]`,
  `Smbs_SelfSignedCertificateNotTrusted_Exits60` (each row and its expected exit code in `Smb.md`). Surl
  commit `4e626fa87f39c6c5d141a3bac7b5f9353ab7338c`.
- **Command line, e.g.:** `curl -sS -m 20 -u alice:secret smb://127.0.0.1:<port>/files/hello.txt`.
- **Upstream build:** static OpenSSL build, SHA-256 `589C8E4D…2648` (built with SMB; the reference build
  has none).
- **Upstream result:** logs in with NTLMv1, downloads and uploads: exit 0 writing `hello smb`; 67 for a
  wrong login; 78 for a missing share or file; 9, 25 and 3 where the rows say; 60 for an untrusted
  `smbs` certificate.
- **Port result:** exit 1, `curl: (1) Protocol "smb" is disabled` (`"smbs"`) in every row, without
  connecting. At port commit `b42c319`, `Curl.Engine.UnitLibrary/EngineComposition.cs` registers `smb`
  and `smbs` with a `DisabledProtocolHandler`.
- **What decides:** curl 8.21.0 `docs/libcurl/opts/CURLOPT_PROTOCOLS_STR.md` (`SMB`, `SMBS`),
  `docs/URL-SYNTAX.md` ("SMB"); the port's own rule that nothing is left out.

### Acceptance criteria

- [ ] `curl -V` lists `smb` and `smbs` in `Protocols:`.
- [ ] `curl -u alice:secret smb://<host>/files/hello.txt` against an SMB server downloads the file
      (exit 0), and a wrong password exits 67 (`CURLE_LOGIN_DENIED`), shown by port tests.
- [ ] A missing share or file exits 78 (`CURLE_REMOTE_FILE_NOT_FOUND`) and `-T` uploads a file, shown by
      port tests.

## 10. Answer an RTSP Digest challenge on the same connection

- **title:** Answer an RTSP Digest challenge on the same connection
- **priority:** Normal · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

With `--digest` or `--anyauth` on an `rtsp://` URL, the port answers a `401` with a
`WWW-Authenticate: Digest` challenge by re-sending the request with `Authorization: Digest …` on the same
connection, as upstream curl 8.21.0 does.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Rtsp.md`, RTSP-1.
- **Cases (3):** `CurlPortBesideUpstreamCurlOverRtspTests.Options_Digest_Exits0OnOneConnection`,
  `Options_DigestWrongPassword_Exits22With401`, `Options_AnyAuth_Exits0WithDigest`. Surl commit
  `fdb0ab43767137d4ce308750d887b2fb2b2ab625`.
- **Command lines** (against `surl rtsp://… --user tester:secret`):
  - `curl -sS -m 20 -f --digest -u tester:secret -w %{num_connects} rtsp://127.0.0.1:<port>/clip.bin`
  - `curl -sS -m 20 -f --digest -u tester:wrong -w %{response_code} rtsp://127.0.0.1:<port>/clip.bin`
  - `curl -sS -m 20 -v -f --anyauth -u tester:secret rtsp://127.0.0.1:<port>/clip.bin`
- **Upstream build:** reference build, SHA-256 `0E773709…8778`.
- **Upstream result:** `OPTIONS *` without credentials, `401`, a second `OPTIONS *` with
  `Authorization: Digest …` on the same connection; exit 0 (`-w %{num_connects}` writes `1`), or 22 with
  `401` for the wrong password.
- **Port result:** sends the first `OPTIONS *` only and stops; first differing request byte: connection 0,
  offset 57 (upstream `0x4F`, the second request's `O`; the port's bytes end). Exit 22 where upstream
  exits 0.
- **What decides:** RFC 2326 section 14 takes RTSP's authentication from HTTP; RFC 7616 section 3.4;
  curl 8.21.0 `lib/rtsp.c` builds every request through `Curl_http_output_auth` and follows a 401.

### Acceptance criteria

- [ ] `--digest -u tester:secret` on `rtsp://` against a server that challenges with Digest exits 0 on one
      connection (`-w %{num_connects}` writes `1`), shown by a port test.
- [ ] `--anyauth` picks Digest from the challenge and succeeds, and a wrong password exits 22 after the
      second request, shown by port tests.

## 11. Offer h2 by ALPN on https by default

- **title:** Offer h2 by ALPN on https by default
- **priority:** High · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

On an `https://` URL with no HTTP version option, the port offers `h2,http/1.1` in the TLS ALPN extension
and speaks HTTP/2 when the server accepts `h2`, as upstream curl 8.21.0 does by default.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http2.md`, finding 1. Related: entry 13.
- **Cases (9):** every `https` case of `CurlPortBesideUpstreamCurlOverHttp2Tests`, e.g.
  `Get_HttpsByAlpn_IsServedOverHttp2`, `Head_HttpsByAlpn_ExitsZeroWithAnHttp2OkHead`,
  `Get_TwoUrls_ReuseOneHttp2Connection` (all listed in `Http2.md`). Surl commit
  `6268bee500368fa80fd2284c689e5e40b15301a0`.
- **Command line, e.g.:** `curl -sS -k -w %{http_version} https://127.0.0.1:<port>/hello.txt` against
  `surl --self-signed https://127.0.0.1:0/`.
- **Upstream build:** static OpenSSL build, SHA-256 `589C8E4D…2648`.
- **Upstream result:** exit 0; `-w %{http_version}` writes `2`; `-I` line 1 `HTTP/2 200 `; `-v` shows
  `* ALPN: curl offers h2,http/1.1`, `* ALPN: server accepted h2`.
- **Port result:** exit 0 with the same bodies; writes `1.1`; `-I` line 1 `HTTP/1.1 200 OK`; `-v` shows
  `* ALPN: curl offers http/1.1`. The port's `-V` lists no `HTTP2` feature. Its `h2c` cases on `http`
  agree with upstream, so its HTTP/2 framing is not at fault.
- **What decides:** curl 8.21.0 manual, `--http2`: "For HTTPS, this means curl negotiates HTTP/2 in the
  TLS handshake. curl does this by default"; RFC 9113 section 3.2; RFC 7301.

### Acceptance criteria

- [ ] `curl -k -w %{http_version} https://<h2 server>/` writes `2`, and `-v` shows
      `* ALPN: curl offers h2,http/1.1`, shown by port tests.
- [ ] `--http1.1` still offers only `http/1.1`, and `curl -V` lists `HTTP2` in `Features:`.

## 12. Support --http3 and --http3-only

- **title:** Support --http3 and --http3-only
- **priority:** Normal · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

The port speaks HTTP/3 over QUIC: `--http3-only` uses HTTP/3 with no fallback, `--http3` falls back to
TCP when QUIC fails, and `curl -V` lists `HTTP3`, as upstream curl 8.21.0 built with ngtcp2 and nghttp3
does.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http3.md`, finding 1.
- **Cases (12):** every `CurlPortBesideUpstreamCurlOverHttp3Tests` case but
  `Head_OverTcpWithHttp3On_CarriesAltSvcNamingThePort`, e.g. `GetWithHttp3Only_File_IsServedOverHttp3`,
  `GetWithHttp3Only_Http3Off_FailsToConnect`, `GetWithHttp3_Http3Off_FallsBackToHttp2`. Surl commit
  `883c3d4d4881bb59b9179bd1cb3f33973096af7f`.
- **Command line, e.g.:** `curl -sS -k --http3-only -w %{http_version} https://127.0.0.1:<port>/hello.txt`
  against `surl --http3`.
- **Upstream build:** static OpenSSL build, SHA-256 `589C8E4D…2648`.
- **Upstream result:** exit 0, `hello from surl\n3`; with `surl` started without `--http3`,
  `--http3-only` exits 56 (`Failed to connect … Failure when receiving data from the peer`) and `--http3`
  falls back over TCP (exit 0, `…\n2`).
- **Port result:** exit 2 in every case, no output,
  `curl: option --http3-only: the installed libcurl version does not support this` (or `--http3`).
  Not a release difference: curl 8.22.0 did not drop HTTP/3; the port's build lacks the feature.
- **What decides:** curl 8.21.0 manual, `--http3-only` and `--http3`; RFC 9114, RFC 9000, RFC 9001; the
  port's own rule that QUIC and HTTP/3 are built by hand.

### Acceptance criteria

- [ ] `curl -V` lists `HTTP3` in `Features:`.
- [ ] `--http3-only` against an HTTP/3 server exits 0 and `-w %{http_version}` writes `3`; against a
      server with no QUIC listener it exits 56 (`CURLE_RECV_ERROR`), shown by port tests.
- [ ] `--http3` against a server with no QUIC listener falls back over TCP and exits 0, shown by a port
      test.

## 13. Negotiate HTTP/2 over https when the server also advertises h3

- **title:** Negotiate HTTP/2 over https when the server also advertises h3
- **priority:** Low · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

`curl -k -I https://…` against a server that also listens for HTTP/3 gets an `HTTP/2 200` head carrying
`alt-svc: h3=":<port>"`, as upstream curl 8.21.0 does; the same defect as entry 11, recorded in the
HTTP/3 family because its mirrored test pins the `HTTP/2 200` line.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http3.md`, finding 2. Fixing entry 11 is expected
  to fix this one; the port's planner may make it depend on entry 11's task.
- **Case:** `CurlPortBesideUpstreamCurlOverHttp3Tests.Head_OverTcpWithHttp3On_CarriesAltSvcNamingThePort`.
  Surl commit `883c3d4d4881bb59b9179bd1cb3f33973096af7f`.
- **Command line:** `curl -sS -k -I https://127.0.0.1:<port>/hello.txt` against `surl --http3`.
- **Upstream build:** static OpenSSL build, SHA-256 `589C8E4D…2648`.
- **Upstream result:** exit 0; line 1 `HTTP/2 200 `, lower-case fields ending
  `alt-svc: h3=":<port>"; ma=86400`.
- **Port result:** exit 0; line 1 `HTTP/1.1 200 OK`, then `Date:` … `Alt-Svc: h3=":<port>"; ma=86400`.
- **What decides:** curl 8.21.0 manual, `--http2` ("curl does this by default"); RFC 9113 section 3.2;
  RFC 7301.

### Acceptance criteria

- [ ] `curl -k -I https://<server with h2 and h3>/` writes `HTTP/2 200 ` as its first line, with the
      `alt-svc` field in lower case, shown by a port test.

## 14. Keep an RTSP session and read interleaved data on its connection in CURL_RTSPREQ_RECEIVE

- **title:** Keep an RTSP session and read interleaved data on its connection in CURL_RTSPREQ_RECEIVE
- **priority:** Normal · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

In the port's managed `Libcurl`, a `CURL_RTSPREQ_RECEIVE` after `SETUP` and `PLAY` reads the interleaved
RTP and RTCP data on the session's existing RTSP connection, one frame per call as upstream libcurl
8.21.0 delivers them, keeps `CURLINFO_RTSP_SESSION_ID` and the CSeq counters, and waits until
`CURLOPT_TIMEOUT` (28) when nothing arrives.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Libcurl.md`, finding 1.
- **Case:** `CurlPortLibcurlBesideUpstreamLibcurlOverRtspTests.PlayThenReceive_ServedFile_DeliversItsBytesThenAnRtcpBye`,
  mirroring `PinnedLibcurlTalksToSurlOverRtspTests` of the same name. Surl commit
  `2006b0770c8c905bd36f4db799c7fcdd7dd3e0e2`.
- **Command line:** `Run-LibcurlRtspScript.cs --timeout 3000 rtsp://{surl}/clip.bin stream-uri:rtsp://{surl}/clip.bin transport:RTP/AVP/TCP;interleaved=0-1 SETUP PLAY RECEIVE RECEIVE RECEIVE`
  (the port's twin `Run-CurlPortLibcurlRtspScript.cs` with the same arguments), against `surl rtsp://…`
  serving a 100-byte `clip.bin`.
- **Builds:** upstream `libcurl-4.dll` SHA-256 `799F7EEF…E574`; port `Libcurl.dll` SHA-256 `613976A7…8311`.
- **Upstream result:** both exit 0. First `RECEIVE`: `session "<session>", cseq received 2, next cseq 3`,
  one interleave call of 116 bytes (the RTP frame); second delivers the 40-byte RTCP frame; third ends
  `CURLcode 28 (Timeout was reached)`. `surl` sees one connection.
- **Port result:** first `RECEIVE` (report line 5): `session none, cseq received 0, next cseq 0`,
  two interleave calls (116 and 40 bytes); second and third return `CURLcode 0` at once with nothing.
  `surl` notes Exchanges 2 and 3: the port's `RECEIVE`s open new connections.
- **What decides:** libcurl 8.21.0 `CURLOPT_RTSP_REQUEST` (https://curl.se/libcurl/c/CURLOPT_RTSP_REQUEST.html),
  `CURLINFO_RTSP_SESSION_ID`, `CURLINFO_RTSP_CSEQ_RECV`, `CURLINFO_RTSP_CLIENT_CSEQ`; RFC 2326 section 10.12.

### Acceptance criteria

- [ ] After `SETUP` and `PLAY`, `CURL_RTSPREQ_RECEIVE` reads on the same connection (no new connection
      opened) and keeps `CURLINFO_RTSP_SESSION_ID` and both CSeq counters, shown by a port test.
- [ ] Each `RECEIVE` hands the interleave callback the frames available, and a `RECEIVE` with nothing to
      read returns `CURLE_OPERATION_TIMEDOUT` (28) after `CURLOPT_TIMEOUT`, shown by a port test.

## 15. Save a cookie whose Domain equals the host as a domain cookie

- **title:** Save a cookie whose Domain equals the host as a domain cookie
- **priority:** Normal · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

A cookie set with a `Domain` attribute equal to the request host is stored as a domain cookie and
written to the jar with a leading dot and the include-subdomains flag `TRUE`, as upstream curl 8.21.0
does, so upstream test cases 798 and 1136 pass with the port.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Upstream-Test-Cases.md`, UTC-1.
- **Cases (2):** `test798` "HTTP cookies in a folded header", `test1136` "Check cookies against PSL"
  (upstream tag `curl-8_21_0`), played by `CurlPortBesideUpstreamCurlOverUpstreamTestCasesTests`. Surl
  commit `7bf55f26a8eecda5f936db7e32f596770ee7adf5`.
- **Command lines:** `curl http://localhost:<port>/we/want/798 -b none -c log/jar798.txt`;
  `curl http://www.example.ck/1136 http://www.ck/1136 http://z-1.compute-1.amazonaws.com/1136 -b none -c log/jar1136.txt -x 127.0.0.1:<port>`.
- **Upstream build:** reference build, SHA-256 `0E773709…8778`.
- **Upstream result:** both cases pass; jar lines `#HttpOnly_.localhost	TRUE	/p4	TRUE	0	flavor	tasty`
  and `.z-1.compute-1.amazonaws.com	TRUE	/	FALSE	0	test5	forbidden5`.
- **Port result:** `<file>` fails at jar line 5: `#HttpOnly_localhost	FALSE	/p4	…` and
  `z-1.compute-1.amazonaws.com	FALSE	/	…` (no leading dot, flag `FALSE`).
- **What decides:** RFC 6265 section 5.3 step 6; curl 8.21.0 `lib/cookie.c` (`tailmatch` set whenever a
  `domain=` is accepted; `get_netscape_format`); the cases' `<file>` sections.

### Acceptance criteria

- [ ] Upstream test cases 798 and 1136 pass with the port in its own upstream-case runner.
- [ ] A port unit test shows a cookie with `domain=<the host>` written as `.<host>	TRUE	…`.

## 16. Send an empty SASL EXTERNAL response as =

- **title:** Send an empty SASL EXTERNAL response as =
- **priority:** Normal · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

With `AUTH=EXTERNAL` and no user name, the port answers the server's empty challenge with the line `=`
over IMAP, POP3 and SMTP, as upstream curl 8.21.0 does, so upstream test cases 838, 884 and 943 pass.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Upstream-Test-Cases.md`, UTC-2.
- **Cases (3):** `test838` (IMAP), `test884` (POP3), `test943` (SMTP), "external authentication without
  credentials". Surl commit `7bf55f26a8eecda5f936db7e32f596770ee7adf5`.
- **Command lines:** `curl 'imap://;AUTH=EXTERNAL@127.0.0.1:<port>/838/;MAILINDEX=1'`;
  `curl 'pop3://;AUTH=EXTERNAL@127.0.0.1:<port>/884'`;
  `curl 'smtp://;AUTH=EXTERNAL@127.0.0.1:<port>/943' --mail-rcpt recipient@example.com --mail-from sender@example.com -T -`.
- **Upstream build:** reference build, SHA-256 `0E773709…8778`.
- **Upstream result:** sends `=` as the third protocol line and exits 0; all three cases pass.
- **Port result:** sends an empty line (`\r\n`) where `=\r\n` is expected (POP3, SMTP `<protocol>`
  line 3); over IMAP the server half answers only `=`, so the port waits until the case's 60-second limit.
- **What decides:** RFC 4422 section 4; RFC 4954 section 4, RFC 5034 section 4, RFC 4959 section 3;
  curl 8.21.0 `lib/curl_sasl.c` (`build_message` with an empty buffer sends `=`).

### Acceptance criteria

- [ ] Upstream test cases 838, 884 and 943 pass with the port in its own upstream-case runner.
- [ ] A port unit test shows the SASL EXTERNAL response with no user name encoded as `=`.

## 17. Write --libcurl's setopt calls in upstream's order

- **title:** Write --libcurl's setopt calls in upstream's order
- **priority:** Low · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

The C program `--libcurl` writes lists its `curl_easy_setopt` calls in the order upstream curl 8.21.0's
tool sets them, so upstream test cases 1402, 1404, 1405, 1406, 1407 and 1465 pass with the port.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Upstream-Test-Cases.md`, UTC-3.
- **Cases (6):** `test1402`, `test1404`, `test1405`, `test1406`, `test1407`, `test1465`. Surl commit
  `7bf55f26a8eecda5f936db7e32f596770ee7adf5`.
- **Command line, e.g.:** `curl http://127.0.0.1:<port>/we/want/1402 --libcurl log/test1402.c -d "foo=bar" -d "baz=quux"`;
  `curl pop3://127.0.0.1:<port>/1407 -l -u user:secret --libcurl log/test1407.c`.
- **Upstream build:** reference build, SHA-256 `0E773709…8778`.
- **Upstream result:** all six pass; the generated file matches the case's `<file>`.
- **Port result:** the transfers agree (`stdout`, `protocol`, `data`, `upload` pass); the generated file
  differs, first at: test1402 line 16 (`CURLOPT_USERAGENT` where `CURLOPT_POSTFIELDS` is expected);
  test1404 line 27 (`CURLOPT_USERAGENT` where `mime1 = curl_mime_init(curl);`); test1405 line 28
  (`CURLOPT_QUOTE` where `CURLOPT_FTP_SKIP_PASV_IP`); test1406 line 21 (`CURLOPT_USERAGENT` where
  `CURLOPT_UPLOAD`); test1407 line 16 (`CURLOPT_USERPWD` where `CURLOPT_DIRLISTONLY`); test1465 line 16
  (`CURLOPT_USERAGENT` where the binary `CURLOPT_POSTFIELDS`).
- **What decides:** curl 8.21.0 `docs/cmdline-opts/libcurl.md`; the cases' `<file>` sections
  (`tests/data/data1402.c` and the rest) pin the exact text, option order included.

### Acceptance criteria

- [ ] Upstream test cases 1402, 1404, 1405, 1406, 1407 and 1465 pass with the port in its own
      upstream-case runner.

---

## 18. Fail the webtransport transfer when the server refuses the CONNECT

*Judged by the specification* (extended client; no upstream curl build has the `webtransport` scheme,
ADR-0083).

- **title:** Fail the webtransport transfer when the server refuses the CONNECT
- **priority:** Normal · **assignee:** Claude · **pipeline:** feature · **depends-on:** [] ·
  **requirement:** none · **id**, **touches:** left to the port's planner

### Goal

When a server answers the port's `webtransport://` extended CONNECT with a non-2xx status (for example
`401`), no session exists, and the port's curl exits with a non-zero exit code and says on stderr that the
server refused the session, instead of exiting 0 with empty output.

### Context

- **Finding:** Surl `Documentation/Conformance/CurlPort/Extended.md`, EXT-1, recorded by Surl task BL-462.
- **Case:** Surl test `PinnedCurlPortClientTalksToSurlOverWebTransportTests.Connect_WithoutCredentials_EchoesNothing`
  (`Integration`), Surl commit `477dda7d5e31ed2a906b2505853800d227260dc9`.
- **Server:** `surl webtransport://127.0.0.1:<port>/ --self-signed --user alice:secret`.
- **Command line:** `curl -k -d "hello refused" webtransport://127.0.0.1:<port>/` (no credentials).
- **Upstream build:** none; upstream curl has no `webtransport` scheme.
- **Server's answer:** `:status 401` with its challenges, then FIN on the CONNECT stream (Surl's row
  `WebTransport:Surl-7-1`, proved by its unit tests).
- **Port result:** exit code 0, empty stdout.
- **What decides:** draft-ietf-webtrans-http3-16 section 3.2: only a 2xx answer to the extended CONNECT
  establishes a session. Upstream curl's nearest behaviour: a refused WebSocket upgrade fails the
  transfer rather than exiting 0 (curl 8.21.0 `lib/ws.c`, "Refused WebSockets upgrade";
  https://curl.se/libcurl/c/libcurl-errors.html). Which exit code to use is the port's call.

### Acceptance criteria

- [ ] A port test where the server answers the webtransport CONNECT `401` asserts a non-zero exit code
      and a stderr message naming the refusal.
- [ ] A port test where the server answers the CONNECT `200` still exits 0 and echoes as before.
- [ ] The chosen exit code is recorded in the port's documentation for the `webtransport` scheme.
