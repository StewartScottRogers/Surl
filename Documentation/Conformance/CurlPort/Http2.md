# Phase 7 findings: HTTP/2

The Curl port measured beside pinned upstream curl against a live `surl` (ADR-0089), by
`Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttp2Tests.cs` (BL-589). It mirrors
every test of `UpstreamCurlFetchesFromSurlOverHttp2Tests` (BL-371): prior knowledge and the `h2c` Upgrade
on `http`, and ALPN `h2` on `https`. Upstream curl is right (ADR-0003 decision 6): each finding below is
the port's defect, for the port's own board (BL-592's handoff), and Surl does not change for it.

## What was measured

- **Platform:** `win-x64`, 2026-11-05. Phase 7 runs on Windows only (ADR-0089 decision 8).
- **Curl port:** commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256
  `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`).
- **Upstream curl:** the static OpenSSL build the mirrored class runs on Windows, since the reference
  build has no `HTTP2` feature (ADR-0081 decision 1): SHA-256
  `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`,
  `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0`.
- **Surl:** commit `6268bee500368fa80fd2284c689e5e40b15301a0` with BL-589's conformance test.

On `http` the bytes each client sent are compared as decoded HTTP/2 frames, not byte for byte
(ADR-0089 amendment 4). On `https` they are TLS's and are not compared (decision 4).

15 cases ran, three times, with the same verdicts each time. The 6 `http` cases (prior knowledge and
the `h2c` Upgrade) agree: same exit code, output and `surl` notes, and the same decoded frames (the
same `SETTINGS`, connection `WINDOW_UPDATE`, decoded request header list and `GOAWAY`). The 9 `https`
cases are one port defect, below. No case showed a Surl defect.

## Findings

The test reads this table: its first column is the Phase 7 case's name (ADR-0089 decision 5). Each
case mirrors the `UpstreamCurlFetchesFromSurlOverHttp2Tests` test of the same name.

| Test | Mirrors | Finding |
| --- | --- | --- |
| `Get_HttpsByAlpn_IsServedOverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.Get_HttpsByAlpn_IsServedOverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `Head_HttpsByAlpn_ExitsZeroWithAnHttp2OkHead` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.Head_HttpsByAlpn_ExitsZeroWithAnHttp2OkHead` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `GetWithRange_HttpsByAlpn_IsServedTheFirstTenBytesWith206OverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithRange_HttpsByAlpn_IsServedTheFirstTenBytesWith206OverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `GetWithBasic_HttpsByAlpn_IsServedOverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithBasic_HttpsByAlpn_IsServedOverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `GetWithBasic_WrongPassword_IsRefusedWith401OverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithBasic_WrongPassword_IsRefusedWith401OverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `GetWithDigest_HttpsByAlpn_IsServedOverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithDigest_HttpsByAlpn_IsServedOverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `Upload_AllowUploads_IsStoredWith201OverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.Upload_AllowUploads_IsStoredWith201OverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `Get_FileLargerThanTheInitialWindow_IsServedWholeOverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.Get_FileLargerThanTheInitialWindow_IsServedWholeOverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `Get_TwoUrls_ReuseOneHttp2Connection` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.Get_TwoUrls_ReuseOneHttp2Connection` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |

### 1. The port does not offer h2 by ALPN on https

- **Command lines** (every `https` case), each against `surl --self-signed https://127.0.0.1:0/`, with
  `--user-file <tester:secret>` for the logins and `--allow-uploads` for the upload:
  - `curl -sS -k -w %{http_version} {surl}/hello.txt` and `{surl}/large.bin`
  - `curl -sS -k -I {surl}/hello.txt`
  - `curl -sS -k -r 0-9 -w " %{response_code} %{http_version}" {surl}/hello.txt`
  - `curl -sS -f -k --basic -u tester:secret -w %{http_version} {surl}/hello.txt`, the same with `--digest`
  - `curl -sS -k --basic -u tester:wrong -o NUL -w "%{response_code} %{http_version}" {surl}/hello.txt`
  - `curl -sS -k -u tester:secret -T up.bin -o NUL -w "%{response_code} %{http_version}" {surl}/up.bin`
    (262,144 bytes)
  - `curl -sS -k -v -w %{http_version} {surl}/hello.txt {surl}/hello.txt`
- **Exit code:** both 0 in every case. The port's transfers complete: same bodies, same status codes,
  and the uploaded file is stored byte for byte the same.
- **First differing item:** standard output, where the HTTP version shows. Upstream `2`, port `1.1`:
  `-w %{http_version}` writes `2` against `1.1` (`hello from 206 2` against `hello from 206 1.1`,
  `401 2` against `401 1.1`, `201 2` against `201 1.1`); under `-I` line 1 is `HTTP/2 200 ` against
  `HTTP/1.1 200 OK`.
- **What `-v` shows** (the two-URL case): upstream `* ALPN: curl offers h2,http/1.1`,
  `* ALPN: server accepted h2`, `* using HTTP/2`; the port `* ALPN: curl offers http/1.1`,
  `* ALPN: server accepted http/1.1`, `* using HTTP/1.x`. Both reuse the one connection for the second URL.
- **Right behaviour:** curl 8.21.0's `--http2` documentation says "For HTTPS, this means curl negotiates
  HTTP/2 in the TLS handshake. curl does this by default", and `--http1.1` is the option that stops it.
  RFC 9113 section 3.2 has a client select HTTP/2 over TLS by offering `h2` in the TLS ALPN extension
  (RFC 7301). The port with no option offers only `http/1.1`, so it never speaks HTTP/2 over TLS
  (its `h2c` cases on `http` agree with upstream, so its HTTP/2 framing itself is not at fault here).
