# Phase 7 findings: HTTP/3

The Curl port measured beside pinned upstream curl against a live `surl --http3` (ADR-0089), by
`Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttp3Tests.cs` (BL-590). It mirrors
every test of `UpstreamCurlFetchesFromSurlOverHttp3Tests` (BL-385): `--http3-only`, `--http3`, and the
`Alt-Svc` header over TCP. Upstream curl is right (ADR-0003 decision 6): each finding below is the port's
defect, for the port's own board (BL-592's handoff), and Surl does not change for it.

## What was measured

- **Platform:** `win-x64`, 2026-11-05. Phase 7 runs on Windows only (ADR-0089 decision 8).
- **Curl port:** commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256
  `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`). Its `-V`:
  `curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel`, features
  `alt-svc AsynchDNS brotli HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd`
  (no `HTTP2`, no `HTTP3`).
- **Upstream curl:** the stunnel/static-curl build the mirrored test runs on Windows (ADR-0082 decision 1),
  SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`,
  `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0`.
  Off Windows the mirrored test runs the reference build, and so would this class once a port pin exists there.
- **Surl:** commit `883c3d4d4881bb59b9179bd1cb3f33973096af7f` with BL-590's test class.

## Result

13 cases on Windows: 13 port defects, no agreement, no Surl defect (upstream's run was what the mirrored
test asserts in every case), none not comparable.

Compared, per ADR-0089 decision 4: exit code, standard output, the standard error lines the mirrored test
pins (`* Reusing existing https: connection with host 127.0.0.1` and `Failed to connect to 127.0.0.1`), the
stored upload, and `surl`'s `-v` notes without how a TLS connection ended (Amendment 1). Request bytes are
not compared: HTTP/3 travels over QUIC, which is UDP and encrypted, and decision 4 leaves request bytes over
UDP and inside TLS uncompared. The QUIC Initial ClientHello is not decoded for either client, for the same
reason decision 4 gives for a TLS ClientHello: it is the TLS library's, not curl's.

The port's version line names 8.22.0 where the paired upstream build is 8.21.0. Neither finding is a release
difference (ADR-0089 decision 2): curl 8.22.0 did not drop HTTP/2 or HTTP/3, both are build features the
port's build lacks.

## Findings

The test reads this table: its first column is the Phase 7 case's name (ADR-0089 decision 5).

| Test | Mirrors | Finding |
| --- | --- | --- |
| `GetWithHttp3Only_File_IsServedOverHttp3` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3Only_File_IsServedOverHttp3` | [1](#1-the-port-has-no-http3) |
| `GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback` | [1](#1-the-port-has-no-http3) |
| `Head_Http3Only_ExitsZeroWithAnHttp3OkHead` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.Head_Http3Only_ExitsZeroWithAnHttp3OkHead` | [1](#1-the-port-has-no-http3) |
| `GetWithRange_Http3Only_IsServedTheFirstTenBytesWith206OverHttp3` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithRange_Http3Only_IsServedTheFirstTenBytesWith206OverHttp3` | [1](#1-the-port-has-no-http3) |
| `GetWithBasic_Http3Only_IsServedOverHttp3` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithBasic_Http3Only_IsServedOverHttp3` | [1](#1-the-port-has-no-http3) |
| `GetWithBasic_WrongPassword_IsRefusedWith401OverHttp3` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithBasic_WrongPassword_IsRefusedWith401OverHttp3` | [1](#1-the-port-has-no-http3) |
| `GetWithDigest_Http3Only_IsServedOverHttp3` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithDigest_Http3Only_IsServedOverHttp3` | [1](#1-the-port-has-no-http3) |
| `Upload_AllowUploads_IsStoredWith201OverHttp3` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.Upload_AllowUploads_IsStoredWith201OverHttp3` | [1](#1-the-port-has-no-http3) |
| `GetWithHttp3Only_TenMebibyteFile_IsServedWholeOverHttp3` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3Only_TenMebibyteFile_IsServedWholeOverHttp3` | [1](#1-the-port-has-no-http3) |
| `GetWithHttp3Only_TwoUrls_ReuseOneQuicConnection` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3Only_TwoUrls_ReuseOneQuicConnection` | [1](#1-the-port-has-no-http3) |
| `GetWithHttp3Only_Http3Off_FailsToConnect` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3Only_Http3Off_FailsToConnect` | [1](#1-the-port-has-no-http3) |
| `GetWithHttp3_Http3Off_FallsBackToHttp2` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3_Http3Off_FallsBackToHttp2` | [1](#1-the-port-has-no-http3) |
| `Head_OverTcpWithHttp3On_CarriesAltSvcNamingThePort` | `UpstreamCurlFetchesFromSurlOverHttp3Tests.Head_OverTcpWithHttp3On_CarriesAltSvcNamingThePort` | [2](#2-the-port-does-not-negotiate-http2-over-https) |

### 1. The port has no HTTP/3

- **Cases:** the twelve rows above that name finding 1; every command line carries `--http3-only` or
  `--http3`, e.g. `curl -sS -k --http3-only -w %{http_version} https://127.0.0.1:<port>/hello.txt`.
- **Upstream:** connects over QUIC and is served over HTTP/3: exit 0, standard output `hello from surl\n3`
  (the body, then `%{http_version}`). With `surl` started without `--http3`, `--http3-only` exits 56 with
  `curl: (56) Failed to connect to 127.0.0.1:<port> after … ms: Failure when receiving data from the peer`,
  and `--http3` falls back over TCP (exit 0, `hello from surl\n2`).
- **Port:** exit 2 in every case, no standard output, standard error
  `curl: option --http3-only: the installed libcurl version does not support this` (or `--http3`), then
  `curl: try 'curl --help' or 'curl --manual' for more information`. Its `-V` lists no `HTTP3` feature.
- **The right behaviour:** curl 8.21.0's manual, `--http3-only`: "Instructs curl to use HTTP/3 to the
  host in the URL, with no fallback to earlier HTTP versions"; `--http3`: "Attempt HTTP/3 to the host in
  the URL, but fallback to earlier HTTP versions if the HTTP/3 connection establishment fails". HTTP/3 is
  RFC 9114 over QUIC (RFC 9000, RFC 9001). The port's standing rule is that nothing is left out because
  the BCL has no primitive for it (its own `CLAUDE.md`): QUIC and HTTP/3 are to be built by hand.

### 2. The port does not negotiate HTTP/2 over HTTPS

- **Case:** `Head_OverTcpWithHttp3On_CarriesAltSvcNamingThePort`,
  `curl -sS -k -I https://127.0.0.1:<port>/hello.txt` against `surl --http3`.
- **Upstream:** exit 0; standard output line 1 `HTTP/2 200 ` (it offers `h2` in ALPN and `surl` takes it),
  then lower-case fields ending `alt-svc: h3=":<port>"; ma=86400`.
- **Port:** exit 0; standard output line 1 `HTTP/1.1 200 OK`, then `Date:` … `Alt-Svc: h3=":<port>"; ma=86400`
  over HTTP/1.1. Its `-V` lists no `HTTP2` feature.
- **The right behaviour:** curl 8.21.0's manual, `--http2`: "For HTTPS, this means curl negotiates
  HTTP/2 in the TLS handshake. curl does this by default." ALPN is RFC 7301; HTTP/2 over TLS is RFC 9113
  section 3.2. This is the same defect any HTTP/2 Phase 7 family (BL-589) sees, recorded here because this
  case's mirrored test pins the `HTTP/2 200` status line.
