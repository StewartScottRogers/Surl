# Phase 7 findings: FTP and FTPS

What `CurlPortBesideUpstreamCurlOverFtpTests` in `Conformance/Surl.Conformance.UnitTests` found when it
ran every FTP and FTPS conversation of `UpstreamCurlFetchesFromSurlOverFtpTests`,
`UpstreamCurlLogsInToSurlOverFtpTests` and `UpstreamCurlUploadsToSurlOverFtpTests` through pinned
upstream curl and the pinned Curl port, each against its own fresh `surl` (ADR-0089, BL-581).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build, `UpstreamCurlBuilds.json`) |
| Surl | commit `ea263ee63d6c8cd04ba122772460b5dcacd63047` |
| Platform | Windows (`win-x64`); off Windows every case is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-05 |

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
