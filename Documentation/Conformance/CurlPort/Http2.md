# Phase 7 findings: HTTP/2

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, which contains BL-1622 and BL-1623; Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`) is the current outcome on linux-x64 and osx-arm64 and supersedes every earlier run named below. Both test jobs ran. This family: linux-x64 5 passed, 10 failed, 0 not executed; osx-arm64 5 passed, 10 failed, 0 not executed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work (BL-1625 classifies osx-arm64, BL-1626 linux-x64), not a CI finding.

The Curl port measured beside pinned upstream curl against a live `surl` (ADR-0089), by
`Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttp2Tests.cs` (BL-589). It mirrors
every test of `UpstreamCurlFetchesFromSurlOverHttp2Tests` (BL-371): prior knowledge and the `h2c` Upgrade
on `http`, and ALPN `h2` on `https`. Upstream curl is right (ADR-0003 decision 6): each finding below is
the port's defect, for the port's own board (BL-592's handoff), and Surl does not change for it.

## What was measured

- **Platform:** `win-x64`, 2026-11-05. Phase 7 runs on Windows only (ADR-0089 decision 8). Re-run 2026-11-16 at port commit 74f3f45: every listed finding still holds, none fixed, none added.
- **Curl port:** commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, `curl.exe` SHA-256
  `7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467` (`CurlPortBuilds.json`).
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
| `Head_HttpsByAlpn_ExitsZeroWithAnHttp2OkHead` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.Head_HttpsByAlpn_ExitsZeroWithAnHttp2OkHead` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |

### Findings (win-x64)

Shown on Windows only (BL-1625, from run 45412760425): on osx-arm64 the port agrees with upstream curl on these eight cases, so the run reported each listing as stale. The linux-x64 job reported the same eight as stale (BL-1626), so the listing is scoped away from linux-x64 too. A heading that names a platform in parentheses applies on that platform alone.

| Test | Mirrors | Finding |
| --- | --- | --- |
| `Get_HttpsByAlpn_IsServedOverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.Get_HttpsByAlpn_IsServedOverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `GetWithRange_HttpsByAlpn_IsServedTheFirstTenBytesWith206OverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithRange_HttpsByAlpn_IsServedTheFirstTenBytesWith206OverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `GetWithBasic_HttpsByAlpn_IsServedOverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithBasic_HttpsByAlpn_IsServedOverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `GetWithBasic_WrongPassword_IsRefusedWith401OverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithBasic_WrongPassword_IsRefusedWith401OverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `GetWithDigest_HttpsByAlpn_IsServedOverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithDigest_HttpsByAlpn_IsServedOverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `Upload_AllowUploads_IsStoredWith201OverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.Upload_AllowUploads_IsStoredWith201OverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `Get_FileLargerThanTheInitialWindow_IsServedWholeOverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.Get_FileLargerThanTheInitialWindow_IsServedWholeOverHttp2` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |
| `Get_TwoUrls_ReuseOneHttp2Connection` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.Get_TwoUrls_ReuseOneHttp2Connection` | [1](#1-the-port-does-not-offer-h2-by-alpn-on-https) |

### Findings (osx-arm64)

Run 45412760425 (BL-1624), classified by BL-1625. The https cases above agree on osx-arm64 except Head_HttpsByAlpn_ExitsZeroWithAnHttp2OkHead, which still shows finding 1 and so stays in the table for every platform.

| Test | Mirrors | Finding |
| --- | --- | --- |
| `GetWithDigest_PriorKnowledge_IsServedOverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithDigest_PriorKnowledge_IsServedOverHttp2` | [2](#2-the-port-resets-stream-1-after-the-digest-401) |

### Findings (linux-x64)

Run 45412760425 (BL-1624), classified by BL-1626: the same extra `RST_STREAM` as on osx-arm64 shows on linux-x64 (reference build `153CA463…4E45`).

| Test | Mirrors | Finding |
| --- | --- | --- |
| `GetWithDigest_PriorKnowledge_IsServedOverHttp2` | `UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithDigest_PriorKnowledge_IsServedOverHttp2` | [2](#2-the-port-resets-stream-1-after-the-digest-401) |
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

### 2. The port resets stream 1 after the Digest 401

- **Case:** `curl -sS -f --http2-prior-knowledge --digest -u tester:secret -w %{http_version} {surl}/hello.txt`
  against `surl --user-file <tester:secret>`, osx-arm64 and linux-x64 (run 45412760425, BL-1625 and BL-1626).
- **Exit code and output:** both 0, same body and version.
- **First differing item:** the decoded request frames (`http`, so frames are compared, ADR-0089 amendment 4).
  After the 401 both clients send the same `SETTINGS` ack and a second `HEADERS` on stream 3 with the Digest
  `authorization`; the port also sends `RST_STREAM stream=1 00000005` (error `STREAM_CLOSED`) between them,
  which upstream does not.
- **Right behaviour:** upstream curl's frames are the reference (ADR-0003): it sends no reset after the 401, so
  the `RST_STREAM` is the port's extra frame. The cause inside the port is not traced.

## osx-arm64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed with the `Phase 7 families (osx-arm64)` job run. Its `phase-7-osx-arm64-results` artifact holds 489 results for all Phase 7 test classes.

- **This family:** 5 passed, 10 failed, 0 not executed.
- **Detail:** Failed: `Get_FileLargerThanTheInitialWindow_IsServedWholeOverHttp2`, `GetWithBasic_WrongPassword_IsRefusedWith401OverHttp2`, `GetWithBasic_HttpsByAlpn_IsServedOverHttp2`, `Get_HttpsByAlpn_IsServedOverHttp2`, `GetWithDigest_HttpsByAlpn_IsServedOverHttp2`, `GetWithDigest_PriorKnowledge_IsServedOverHttp2`, `Upload_AllowUploads_IsStoredWith201OverHttp2`, `Get_TwoUrls_ReuseOneHttp2Connection`, `GetWithRange_HttpsByAlpn_IsServedTheFirstTenBytesWith206OverHttp2`, `GetWithHttp2AndInclude_File_ShowsThe101ThenTheHttp2Head`. All 15 cases executed.
- **Cases compared.** The `UpstreamCurlBuilds.json` pin for `osx-arm64` (curl 8.21.0, SHA-256 `04E0E69B...EEBF`, pinned 2026-09-28) was found, and BL-1622 removed `UnpinnedCurlPortClientException` (357 of them in run 44044472038), so cases that reach the port compared it with upstream curl. An earlier statement that no upstream curl build is pinned for `osx-arm64` was false.
- **Triaged by BL-1625.** Of the 10 failures: 8 were stale listings (the cases agree on osx-arm64), now moved under `### Findings (win-x64)`; `GetWithDigest_PriorKnowledge_IsServedOverHttp2` is a new port defect (finding 2), listed under `### Findings (osx-arm64)`; `GetWithHttp2AndInclude_File_ShowsThe101ThenTheHttp2Head` differed only in the `etag` value, a test defect fixed by normalising `ETag:` (`CurlPortResultNormaliser`). `Head_HttpsByAlpn_ExitsZeroWithAnHttp2OkHead` still shows finding 1 on osx-arm64 and stays listed. The Curl port is never an oracle (ADR-0003).

## linux-x64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed. Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing. It supersedes run 44044472038 and the later scheduled run 45198867449 (2026-11-23).

- **This family:** 5 passed, 10 failed, 0 not executed.
- **Detail:** Failed: `Get_FileLargerThanTheInitialWindow_IsServedWholeOverHttp2`, `GetWithBasic_WrongPassword_IsRefusedWith401OverHttp2`, `GetWithBasic_HttpsByAlpn_IsServedOverHttp2`, `Get_HttpsByAlpn_IsServedOverHttp2`, `GetWithDigest_HttpsByAlpn_IsServedOverHttp2`, `GetWithDigest_PriorKnowledge_IsServedOverHttp2`, `Upload_AllowUploads_IsStoredWith201OverHttp2`, `Get_TwoUrls_ReuseOneHttp2Connection`, `GetWithRange_HttpsByAlpn_IsServedTheFirstTenBytesWith206OverHttp2`, `GetWithHttp2AndInclude_File_ShowsThe101ThenTheHttp2Head`. All 15 cases executed.
- **Triaged by BL-1626.** Of the 10 failures: 8 were stale listings of finding 1 (`Get_HttpsByAlpn`, `GetWithRange_HttpsByAlpn`, `GetWithBasic_HttpsByAlpn`, `GetWithBasic_WrongPassword`, `GetWithDigest_HttpsByAlpn`, `Upload_AllowUploads`, `Get_FileLargerThanTheInitialWindow`, `Get_TwoUrls_ReuseOneHttp2Connection`): on linux-x64, as on osx-arm64, the port offers h2, so they stay under `### Findings (win-x64)` only, as BL-1625 scoped them; `GetWithDigest_PriorKnowledge_IsServedOverHttp2` shows finding 2 on linux-x64 as on osx-arm64, now listed under `### Findings (linux-x64)`; `GetWithHttp2AndInclude_File_ShowsThe101ThenTheHttp2Head` was the `ETag:` test defect BL-1625 fixed. Only `Head_HttpsByAlpn_ExitsZeroWithAnHttp2OkHead` (finding 1, unscoped) still shows. Runs 45198867449 and 44044472038 did not execute these Http2 cases on linux-x64, so no verdict differed.

### Earlier run (44044472038, 2026-11-17)

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339, BL-1307) completed.
Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing.

Findings:

- **Cases compared.** The test job accepted the Curl port hash its own build job reproduced, so no
  `UnpinnedCurlPortClientException` was thrown (it replaces run 44012635963, where nothing compared).
- **This family:** 0 passed, 0 failed, 15 not executed.
- **Triaged by BL-1343.** No case of this family failed on linux-x64, so nothing is added to its findings table.
