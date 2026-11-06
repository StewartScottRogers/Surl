# Phase 7 findings: HTTP/1.1 and HTTPS

The Curl port measured beside pinned upstream curl against a live `surl` (ADR-0089), by
`Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttpTests.cs` (BL-580). It mirrors
every test of `UpstreamCurlFetchesFromSurlOverHttp11Tests`, `UpstreamCurlFetchesFromSurlOverHttpsTests`,
`UpstreamCurlLogsInToSurlOverHttpTests`, `UpstreamCurlTrustsSurlsHostCertificateTests` and
`UpstreamCurlLogsInToSurlWithKerberosTests`. Upstream curl is right (ADR-0003 decision 6): each finding
below is the port's defect, for the port's own board (BL-592's handoff), and Surl does not change for it.

## What was measured

- **Platform:** `win-x64`, 2026-11-05. Phase 7 runs on Windows only (ADR-0089 decision 8).
- **Curl port:** commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256
  `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`).
- **Upstream curl, reference build:** SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`,
  `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP`.
- **Upstream curl, static OpenSSL build** (the Negotiate and Ed25519 host-certificate cases, as their
  mirrored tests run it): SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`,
  `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0`.
- **Surl:** commit `489f8d6f9b80614937f188b1a41896b8c5ed0e5c` with BL-580's changes to the conformance tests.

On Windows 53 cases ran (data rows counted): 43 agree and the 10 below are port defects. No case showed a Surl defect. The
4 cases whose mirrored tests run only off Windows are Inconclusive here.

## Findings

The test reads this table: its first column is the Phase 7 case's name (ADR-0089 decision 5).

| Test | Mirrors | Finding |
| --- | --- | --- |
| `Download_Ed25519HostCertificateOnTheOpenSslWindowsBuild_AgreesThePlainKeyAndExits60` | `UpstreamCurlTrustsSurlsHostCertificateTests.Download_Ed25519HostCertificateOnTheOpenSslWindowsBuild_AgreesThePlainKeyAndExits60` | [1](#1-the-port-offers-no-ed25519-host-key-algorithm) |
| `Download_RsaHostCertificateWithThePlainKeyPinned_DownloadsTheFile` | `UpstreamCurlTrustsSurlsHostCertificateTests.Download_RsaHostCertificateWithThePlainKeyPinned_DownloadsTheFile` | [2](#2-the-port-closes-the-sftp-channel-before-it-disconnects) |
| `GssapiOverSmtp_Account_StoresTheMessageInTheAccountsInbox` | `UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverSmtp_Account_StoresTheMessageInTheAccountsInbox` | [3](#3-the-port-cannot-log-in-with-sasl-gssapi) |
| `GssapiOverSmtp_Account_StoresTheMessageInTheAccountsInbox(with --sasl-ir)` | `UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverSmtp_Account_StoresTheMessageInTheAccountsInbox` | [3](#3-the-port-cannot-log-in-with-sasl-gssapi) |
| `GssapiOverImap_Account_FetchesTheDeliveredMessage` | `UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverImap_Account_FetchesTheDeliveredMessage` | [3](#3-the-port-cannot-log-in-with-sasl-gssapi) |
| `GssapiOverImap_Account_FetchesTheDeliveredMessage(with --sasl-ir)` | `UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverImap_Account_FetchesTheDeliveredMessage` | [3](#3-the-port-cannot-log-in-with-sasl-gssapi) |
| `GssapiOverPop3_Account_RetrievesTheDeliveredMessage` | `UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverPop3_Account_RetrievesTheDeliveredMessage` | [3](#3-the-port-cannot-log-in-with-sasl-gssapi) |
| `GssapiOverPop3_Account_RetrievesTheDeliveredMessage(with --sasl-ir)` | `UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverPop3_Account_RetrievesTheDeliveredMessage` | [3](#3-the-port-cannot-log-in-with-sasl-gssapi) |
| `NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes` | `UpstreamCurlLogsInToSurlOverHttpTests.NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes` | [4](#4-the-ports-negotiate-does-not-fall-back-to-ntlm) |
| `NegotiateOverHttp_WrongPassword_IsRefusedWithHttpReturnedError` | `UpstreamCurlLogsInToSurlOverHttpTests.NegotiateOverHttp_WrongPassword_IsRefusedWithHttpReturnedError` | [4](#4-the-ports-negotiate-does-not-fall-back-to-ntlm) |

### 1. The port offers no Ed25519 host key algorithm

- **Command line:** `curl -sS --knownhosts <known_hosts> -u tester:secret {surl}/a.txt`, against
  `surl -v --hostkey <Ed25519 key> --hostcert <its certificate> sftp://127.0.0.1:0/`; `known_hosts` holds
  only an `@cert-authority [127.0.0.1]:<port>` line. Paired with the static OpenSSL build.
- **Exit code:** upstream 60; port 2.
- **First differing stderr line:** upstream `curl: (60) SSL peer certificate or SSH remote key was not OK`;
  port `curl: (2) Failure establishing ssh session: -5, Unable to exchange encryption keys`.
- **First differing surl note (line 4):** upstream's surl notes
  `SSH negotiated kex curve25519-sha256, host key ssh-ed25519, …`; the port's surl notes
  `SSH no common host key algorithm; client offered rsa-sha2-512,rsa-sha2-256,rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,ssh-rsa,ssh-rsa-cert-v01@openssh.com`
  and disconnects with reason 3. Both clients identify as `SSH-2.0-libssh2_1.11.1`.
- **Right behaviour:** RFC 8709 section 4 defines `ssh-ed25519` as a host key algorithm, and RFC 4253
  section 7.1 has the client list every host key algorithm it supports. Upstream's libssh2 1.11.1 on
  OpenSSL lists `ssh-ed25519`, agrees it with a server that holds only an Ed25519 key, and then refuses
  the key for want of a `known_hosts` entry (60). The port lists RSA names only, so the key exchange
  fails before any host key is judged.

### 2. The port closes the SFTP channel before it disconnects

- **Command line:** `curl -sS --hostpubsha256 <surl's ssh-rsa key SHA-256> -u tester:secret {surl}/a.txt`,
  against `surl -v --hostkey <RSA key> --hostcert <its certificate> sftp://127.0.0.1:0/`. Paired with the
  reference build.
- **Exit code and output:** both 0 with the file's 12 bytes.
- **First differing surl note (line 12; the same in three runs):** after `SFTP CLOSE /a.txt: read 12 bytes`,
  upstream's surl notes `SSH disconnect received: 11 Shutdown`, then `SFTP session ended: client EOF`,
  and no `SSH channel 0 ended` note; the port's surl notes `SFTP session ended: client EOF`,
  `SSH channel 0 ended: exit status 0`, then `SSH disconnect received: 11 Shutdown`. Upstream sends
  `SSH_MSG_DISCONNECT` with the SFTP channel still open; the port closes the channel and waits for its
  exit status first.
- **Right behaviour:** RFC 4254 section 5.3 lets either side close a channel or end the connection, and
  RFC 4253 section 11.1 lets a party disconnect at any time, so both orders are valid SSH; ADR-0089 makes
  upstream curl 8.21.0's order the expected one, since a server that watches the shutdown (as Surl's
  notes do) sees the two clients differently.

### 3. The port cannot log in with SASL GSSAPI

- **Command lines** (each with and without `--sasl-ir`), against `surl --auth gssapi --keytab <keytab>
  --user-file <tester@SURL.TEST>` and the hand-built test KDC on `127.0.0.1:88` (ADR-0065), names pinned
  to loopback with `--resolve mail.surl.test:<port>:127.0.0.1`:
  - `curl -sS -u tester@SURL.TEST:surl-test-password --login-options AUTH=GSSAPI --mail-from a@x --mail-rcpt "tester@SURL.TEST"@example.com -T mail.txt smtp://mail.surl.test:<port>/c`
  - `curl -sS -u tester@SURL.TEST:surl-test-password --login-options AUTH=GSSAPI imap://mail.surl.test:<port>/INBOX;UID=1`
  - `curl -sS -u tester@SURL.TEST:surl-test-password --login-options AUTH=GSSAPI pop3://mail.surl.test:<port>/1`

  The IMAP and POP3 cases read one message delivered beforehand by upstream curl over SMTP, copied
  into each run's data directory so both clients read the same bytes. Paired with the reference build.
- **Exit code:** upstream 0; port 67, stderr `curl: (67) Login denied`, in all six cases.
- **First differing item:** SMTP: the stored message (upstream's inbox holds it; the port's holds
  nothing). IMAP and POP3: standard output line 1, upstream `Return-Path: <a@x>`, port nothing.
- **Right behaviour:** RFC 4752 defines the `GSSAPI` SASL mechanism, which SMTP (RFC 4954), IMAP
  (RFC 9051 section 6.2.2) and POP3 (RFC 5034) carry; curl 8.21.0's `--login-options AUTH=GSSAPI` selects
  it and, on Windows, upstream obtains the Kerberos ticket through SSPI from `-u`'s password. The port
  gives up with 67 instead of completing the exchange.

### 4. The port's Negotiate does not fall back to NTLM

- **Command lines:** `curl -sS --negotiate -u tester:secret {surl}/hello.txt` and
  `curl -sS -f --negotiate -u tester:wrong {surl}/hello.txt`, against
  `surl --auth negotiate --user-file <tester:secret> http://127.0.0.1:0/` on a machine with no Kerberos
  realm for `127.0.0.1`. Paired with the static OpenSSL build, the unpatched 8.21.0 build ADR-0042 pins
  because it still offers NTLM inside SPNEGO.
- **Right password:** both exit 0; upstream writes `hello from surl`, the port writes nothing (it took
  surl's 401 as the answer).
- **Wrong password:** both exit 22; upstream's stderr is `curl: (22) The requested URL returned error: 401`
  (surl refused the NTLM login), the port's is
  `curl: (22) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`.
- **Right behaviour:** RFC 4559 section 4 carries SPNEGO (RFC 4178) tokens in `Negotiate`, and SPNEGO may
  negotiate NTLMSSP when Kerberos is unavailable; curl 8.21.0 as tagged does, which is why its login
  succeeds. The port behaves as the Windows reference build does, which carries upstream's later
  `!ntlm` change (ADR-0040, ADR-0042); the reference build's own case,
  `NegotiateOverHttp_WindowsReferenceBuild_SendsNoTokenAndIsRefused`, agrees with the port. The
  port's maintainer decides which upstream behaviour the port follows; measured against 8.21.0 as
  tagged, it is a difference.
