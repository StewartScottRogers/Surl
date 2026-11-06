# Phase 7 findings: WebSocket

What `CurlPortBesideUpstreamCurlOverWebSocketTests` in `Conformance/Surl.Conformance.UnitTests` found when
it ran every `ws` and `wss` conversation of `UpstreamCurlTalksToSurlOverWebSocketTests` through pinned
upstream curl and the pinned Curl port, each against its own fresh `surl` (ADR-0089, BL-585).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build, `UpstreamCurlBuilds.json`) |
| Surl | commit `c17373cb97858b7f9f944da81eceaa241e56528b` |
| Platform | Windows (`win-x64`); off Windows every case is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-05 |

## Result

20 cases on Windows: 20 agreements, no port defect, no Surl defect, none not comparable.

Compared, per ADR-0089 decision 4: exit code, standard output, the `Refused WebSocket upgrade:` standard
error line of each refusal case (the line the mirrored test pins), `surl`'s `-v` notes, and for every `ws`
case the bytes each client sent: its upgrade request and its frames. Those are compared under ADR-0089
Amendment 1: the `Sec-WebSocket-Key:` value as `<websocket-key>`, and each client frame unmasked with
its masking key zeroed, since both are fresh random values in every run. The six `wss` cases compare
everything but the bytes inside TLS. For `File_WsWithOutput_WritesTheFilesBytesToTheOutputFile` the test
also compares the two runs' `-o` files byte for byte.

The three echo cases run `surl` over an empty served folder where the mirrored test starts it in memory;
`--ws-echo` serves no file, so neither run reads the folder.

## The libcurl rows

`PinnedLibcurlTalksToSurlOverWebSocketTests` (ADR-0071 Amendment 1) drives upstream's `libcurl-4.dll`.
The port's `Libcurl` is a managed API, not a native library, so its rows are measured by BL-597's
port-side driver, not here (ADR-0089 decision 1 (c)), and its findings go to `Libcurl.md`.

## Findings

The test reads the first column of this table (ADR-0089 decision 5): a listed case must still show its
port defect, and an unlisted case must agree.

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |

None: the Curl port agrees with upstream curl on every WebSocket case.
