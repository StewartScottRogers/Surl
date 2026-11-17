# Phase 7 findings: WebSocket

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7` (2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339) is the current linux-x64 outcome and supersedes the run below. Nothing threw `UnpinnedCurlPortClientException`: the test job accepted the hash its own build job reproduced (BL-1340, BL-1339), so cases compared pinned upstream curl with the Curl port. This family: 20 passed, 0 failed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work, not a CI finding.

What `CurlPortBesideUpstreamCurlOverWebSocketTests` in `Conformance/Surl.Conformance.UnitTests` found when
it ran every `ws` and `wss` conversation of `UpstreamCurlTalksToSurlOverWebSocketTests` through pinned
upstream curl and the pinned Curl port, each against its own fresh `surl` (ADR-0089, BL-585).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, `curl.exe` SHA-256 `7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build, `UpstreamCurlBuilds.json`) |
| Surl | commit `c17373cb97858b7f9f944da81eceaa241e56528b` |
| Platform | Windows (`win-x64`); off Windows every case is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-05; re-run 2026-11-16 at port commit 74f3f45: every listed finding still holds, none fixed, none added |

## Result

20 cases on Windows: 20 agreements, no port defect, no Surl defect, none not comparable.

Compared, per ADR-0089 decision 4: exit code, standard output, the `Refused WebSocket upgrade:` standard
error line of each refusal case (the line the mirrored test pins), `surl`'s `-v` notes, and for every `ws`
case the bytes each client sent: its upgrade request and its frames. Those are compared under ADR-0089
Amendment 2: the `Sec-WebSocket-Key:` value as `<websocket-key>`, and each client frame unmasked with
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
- **This family:** 20 passed, 0 failed.
- **Triaged by BL-1343.** No case of this family failed on linux-x64, so nothing is added to its findings table.
