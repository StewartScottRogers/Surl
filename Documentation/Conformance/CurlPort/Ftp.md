# Phase 7 findings: FTP and FTPS

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

None: the Curl port agrees with upstream curl on every FTP and FTPS case.

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
