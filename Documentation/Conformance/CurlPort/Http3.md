# Phase 7 findings: HTTP/3

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7` (2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339) is the current linux-x64 outcome and supersedes the run below. Nothing threw `UnpinnedCurlPortClientException`: the test job accepted the hash its own build job reproduced (BL-1340, BL-1339), so cases compared pinned upstream curl with the Curl port. This family: 12 passed, 1 failed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work, not a CI finding.

The Curl port measured beside pinned upstream curl against a live `surl --http3` (ADR-0089), by
`Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttp3Tests.cs` (BL-590). It mirrors
every test of `UpstreamCurlFetchesFromSurlOverHttp3Tests` (BL-385): `--http3-only`, `--http3`, and the
`Alt-Svc` header over TCP. Upstream curl is right (ADR-0003 decision 6): each finding below is the port's
defect, for the port's own board (BL-592's handoff), and Surl does not change for it.

## What was measured

- **Platform:** `win-x64`, 2026-11-05. Phase 7 runs on Windows only (ADR-0089 decision 8). Re-run 2026-11-16 at port commit 74f3f45: every listed finding still holds, none fixed, none added.
- **Curl port:** commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, `curl.exe` SHA-256
  `7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467` (`CurlPortBuilds.json`). Its `-V`:
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

### Findings (win-x64)

Finding 2 shows on Windows only: on linux-x64 the pinned port agrees with upstream curl on this case (run
44044472038, BL-1343), so the test would fail there on a listed case that agrees. The test reads this table on
Windows only (a heading that names a platform in parentheses applies on that platform alone).

| Test | Mirrors | Finding |
| --- | --- | --- |
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

## osx-arm64

Run [44000208980](https://github.com/StewartScottRogers/Surl/actions/runs/44000208980) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `c87b86d5c2ed723aeaf615bbc980b8d31d44b462`, Curl port commit
`74f3f45c47e86300b2b262dc1dacd43b508b09aa`) completed with the `Phase 7 families (osx-arm64)` job green
(BL-1324). Its `phase-7-osx-arm64-results` artifact holds 489 results for all Phase 7 test classes: 1 passed, 131 not
executed, and every case of this family Inconclusive or failed before any comparison.

Findings:

- **Nothing was compared.** `UpstreamCurlBuilds.json` pins no upstream curl build for `osx-arm64`, so the mirrored
  tests are Inconclusive there with "pins no upstream curl build for osx-arm64" (upstream curl is the only oracle,
  ADR-0003). Pinning one means downloading a build, which needs Stewart's approval (CLAUDE.md, "Decisions").
- **The osx-arm64 port build is not pinned by hash on a CI runner.** The runner's published `curl` hashed
  `71F9BB5EED0DCAB034D33883365082D2BA19758E9549B140037C4E663A98A0D3`, not the pinned
  `B5B48DC7CB11F42F41BD7F45E127ADF08E6BD1A0D3CAA0F80840E1971046BBEE` (`ciHashStable: false`), so cases that reach the port
  throw `UnpinnedCurlPortClientException`. The hash changes with each publish; it is not a port defect.
- **No port defect and no Surl defect is recorded for osx-arm64.** The family's findings table is unchanged.

## linux-x64

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339, BL-1307) completed.
Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing.

Findings:

- **Cases compared.** The test job accepted the Curl port hash its own build job reproduced, so no
  `UnpinnedCurlPortClientException` was thrown (it replaces run 44012635963, where nothing compared).
- **This family:** 12 passed, 1 failed.
- **Triaged by BL-1343.** The 1 failure, `Head_OverTcpWithHttp3On_CarriesAltSvcNamingThePort`, was a listed finding that
  does not show on linux-x64; it now sits in a win-x64-only table (finding 2).
