# Phase 7 findings: FTP and FTPS

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, which contains BL-1622 and BL-1623; Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`) is the current outcome on linux-x64 and osx-arm64 and supersedes every earlier run named below. Both test jobs ran. This family: linux-x64 39 passed, 4 failed, 3 not executed; osx-arm64 43 passed, 0 failed, 3 not executed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work (BL-1625 classifies osx-arm64, BL-1626 linux-x64), not a CI finding.

What `CurlPortBesideUpstreamCurlOverFtpTests` in `Conformance/Surl.Conformance.UnitTests` found when it
ran every FTP and FTPS conversation of `UpstreamCurlFetchesFromSurlOverFtpTests`,
`UpstreamCurlLogsInToSurlOverFtpTests` and `UpstreamCurlUploadsToSurlOverFtpTests` through pinned
upstream curl and the pinned Curl port, each against its own fresh `surl` (ADR-0089, BL-581).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, `curl.exe` SHA-256 `7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build, `UpstreamCurlBuilds.json`) |
| Surl | commit `ea263ee63d6c8cd04ba122772460b5dcacd63047` |
| Platform | Windows (`win-x64`); off Windows every case is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-05; re-run 2026-11-16 at port commit 74f3f45: every listed finding still holds, none fixed, none added |

## Result

43 cases on Windows: 43 agreements, no port defect, no Surl defect, none not comparable. The three
off-Windows listing twins are excluded on Windows by their `[OSCondition]`, as their mirrored tests are.

Compared, per ADR-0089 decision 4: exit code, standard output, the `curl: (N)` standard error line where
the mirrored test pins it, `surl`'s `-v` notes, the bytes each client sent on the control connection
(every `ftp://` case except the three explicit-TLS rows of `Download_AccountOverExplicitTls_WritesTheFile`,
whose bytes after `AUTH TLS` are the TLS library's, and the implicit `ftps://` case), and the stored file
for each upload case. Data connections go straight to the port `surl` announced and are not recorded.

Both runs' served files carry one fixed modification time, so `-I`'s `Last-Modified`, the long listing's
date and `MLSD`'s `modify` fact compare equal although each run's `surl` wrote its content seconds apart.

## Findings

The test reads the first column of this table (ADR-0089 decision 5): a listed case must still show its
port defect, and an unlisted case must agree. A data-driven case is named `Method[data row]`.

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |

None: the Curl port agrees with upstream curl on every FTP and FTPS case on Windows.

### Findings (linux-x64)

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) (BL-1343): four TLS cases
showed one port defect on linux-x64 and are listed for linux-x64 only (a heading that names a platform in
parentheses applies on that platform alone).

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |
| `Download_AccountOverImplicitFtps_WritesTheFile` | `UpstreamCurlLogsInToSurlOverFtpTests.Download_AccountOverImplicitFtps_WritesTheFile` | [FTP-1](#ftp-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Download_AccountOverExplicitTls_WritesTheFile[--ssl-reqd]` | `UpstreamCurlLogsInToSurlOverFtpTests.Download_AccountOverExplicitTls_WritesTheFile` | [FTP-1](#ftp-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Download_AccountOverExplicitTls_WritesTheFile[--ssl-reqd --ftp-ssl-ccc]` | `UpstreamCurlLogsInToSurlOverFtpTests.Download_AccountOverExplicitTls_WritesTheFile` | [FTP-1](#ftp-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Download_AccountOverExplicitTls_WritesTheFile[--ftp-ssl-control]` | `UpstreamCurlLogsInToSurlOverFtpTests.Download_AccountOverExplicitTls_WritesTheFile` | [FTP-1](#ftp-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |

#### FTP-1: upstream curl's TLS session ends with a broken pipe, the port's does not

- **Measured:** exit code, standard output and the stored file agree. The only difference is `surl`'s `-v` note on
  line 3: upstream curl's run has `Exchange 1 ended because the protocol server threw IOException: Unable to write
  data to the transport connection: Broken pipe.`, the port's run has no such note (the same finding as Mail's M-1).
- **Classification:** port defect, in how the port ends a TLS session after the exchange (notes only).

## osx-arm64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed with the `Phase 7 families (osx-arm64)` job run. Its `phase-7-osx-arm64-results` artifact holds 489 results for all Phase 7 test classes.

- **This family:** 43 passed, 0 failed, 3 not executed.
- **Detail:** Not executed: 3, "Test is only supported on Windows".
- **Cases compared.** The `UpstreamCurlBuilds.json` pin for `osx-arm64` (curl 8.21.0, SHA-256 `04E0E69B...EEBF`, pinned 2026-09-28) was found, and BL-1622 removed `UnpinnedCurlPortClientException` (357 of them in run 44044472038), so cases that reach the port compared it with upstream curl. An earlier statement that no upstream curl build is pinned for `osx-arm64` was false.
- **Triaged by BL-1625.** No case of this family failed on osx-arm64, so nothing is added to its findings table. The Curl port is never an oracle (ADR-0003).

## linux-x64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed. Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing. It supersedes run 44044472038 and the later scheduled run 45198867449 (2026-11-23).

- **This family:** 39 passed, 4 failed, 3 not executed.
- **Detail:** Failed: `Download_AccountOverImplicitFtps_WritesTheFile`, `Download_AccountOverExplicitTls_WritesTheFile` (`--ssl-reqd`, `--ssl-reqd --ftp-ssl-ccc`, `--ftp-ssl-control`). Not executed: 3, "Test is only supported on Windows".
- **Not classified here.** BL-1626 classifies the linux-x64 failures.

### Earlier run (44044472038, 2026-11-17)

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339, BL-1307) completed.
Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing.

Findings:

- **Cases compared.** The test job accepted the Curl port hash its own build job reproduced, so no
  `UnpinnedCurlPortClientException` was thrown (it replaces run 44012635963, where nothing compared).
- **This family:** 39 passed, 4 failed, 3 not executed.
- **Triaged by BL-1343.** The 4 failures are listed under "Findings (linux-x64)" above: one port defect (FTP-1).
