# Phase 7 findings: DICT, Gopher, TELNET, TFTP and MQTT

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, which contains BL-1622 and BL-1623; Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`) is the current outcome on linux-x64 and osx-arm64 and supersedes every earlier run named below. Both test jobs ran. This family: linux-x64 26 passed, 0 failed, 0 not executed; osx-arm64 26 passed, 0 failed, 0 not executed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work (BL-1625 classifies osx-arm64, BL-1626 linux-x64), not a CI finding.

What `CurlPortBesideUpstreamCurlOverDictTests`, `CurlPortBesideUpstreamCurlOverGopherTests`,
`CurlPortBesideUpstreamCurlOverTelnetTests`, `CurlPortBesideUpstreamCurlOverTftpTests` and
`CurlPortBesideUpstreamCurlOverMqttTests` in `Conformance/Surl.Conformance.UnitTests` found when they ran
every conversation of `UpstreamCurlQueriesSurlOverDictTests`, `UpstreamCurlFetchesFromSurlOverGopherTests`,
`UpstreamCurlTalksToSurlOverTelnetTests`, `UpstreamCurlFetchesFromSurlOverTftpTests`,
`UpstreamCurlTalksToSurlOverMqttTests` and `UpstreamCurlLogsInToSurlOverMqttTests` through pinned upstream
curl and the pinned Curl port, each against its own fresh `surl` (ADR-0089, BL-584).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, `curl.exe` SHA-256 `7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build, paired for every scheme here) |
| Surl | commit `15a2e03bcff75fbed29d156032d3fb634c59d205` |
| Platform | Windows (`win-x64`); off Windows every case is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-05; re-run 2026-11-16 at port commit 74f3f45: every listed finding still holds, none fixed, none added |

## Result

36 cases in 26 tests on Windows: 36 agreements, no port defect, no Surl defect, none not comparable.
Upstream's run is what the mirrored test asserts in every case. The run was repeated twice with the same
result.

| Family | Tests | Cases | Request bytes compared |
| --- | --- | --- | --- |
| DICT | 1 (5 data rows) | 5 | Yes, every `dict://` connection |
| Gopher | 3 | 3 | Yes, every `gopher://` connection; the mirrored class runs no `gophers://` conversation |
| TELNET | 3 | 3 | No (ADR-0089 amendment 5) |
| TFTP | 6 | 8 | No: UDP (decision 4) |
| MQTT and MQTTS | 9 | 13 | Yes on `mqtt://`; no on `mqtts://` (TLS, decision 4) |

How the mirrored tests became cases:

- A mirrored test that runs curl more than once against one `surl` - an MQTT publish then a subscribe,
  a TFTP upload then a read - is one case per measured run, named `Method[publish]`, `Method[subscribe]`,
  `Method[upload]`, `Method[read]` and `Method[fresh]`. The run that only prepares the next (the publish
  before a measured subscribe, the upload before a measured read) is a set-up step run once through
  upstream curl (amendment 1), so both clients meet the same retained message or stored file.
- The restart cases start the second `surl` inside `CurlPortRunSetup.StartSurlAsync`: over the same data
  directory, or in memory afresh.
- `UploadThenRead_NoDirectory_…[fresh]` expects the exit code upstream curl gets reading a missing file
  from a `surl` over an empty directory, measured in the same test, as the mirrored test does.

What decision 4 compares here, and what amendment 5 changed to compare it truly:

- DICT's `CLIENT` line is curl's self-identification and is normalised like `User-Agent:`. The port
  sends `CLIENT libcurl 8.22.0` where upstream 8.21.0 sends `CLIENT libcurl 8.21.0`; every other byte
  each client sent agrees.
- An MQTT `CONNECT` carries the client identifier `curl` plus 8 random alphanumerics, chosen afresh per
  connection by either client; the 8 characters are normalised. Every other byte agrees.
- A Gopher menu names `surl`'s own listening port, not the recording relay's, so standard output now
  has both ports replaced.
- TELNET request bytes are not compared. Against `surl` upstream sent `hello\nquit\n` before its option
  replies; in its own recording (`Protocols/Terminal/Surl.Protocol.Telnet.UnitTests/Fixtures`) it sent
  them after. The order depends on when the server's options arrive, which is timing.

## Findings

None. The test reads the first column of this table (ADR-0089 decision 5): a listed case must still
show its port defect, and an unlisted case must agree.

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |

## osx-arm64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed with the `Phase 7 families (osx-arm64)` job run. Its `phase-7-osx-arm64-results` artifact holds 489 results for all Phase 7 test classes.

- **This family:** 26 passed, 0 failed, 0 not executed.
- **Detail:** Dict 5, Gopher 3, Telnet 3, Tftp 6, Mqtt 9 passed.
- **Cases compared.** The `UpstreamCurlBuilds.json` pin for `osx-arm64` (curl 8.21.0, SHA-256 `04E0E69B...EEBF`, pinned 2026-09-28) was found, and BL-1622 removed `UnpinnedCurlPortClientException` (357 of them in run 44044472038), so cases that reach the port compared it with upstream curl. An earlier statement that no upstream curl build is pinned for `osx-arm64` was false.
- **No finding is classified here.** Failures are recorded, not classified; BL-1625 classifies the osx-arm64 ones. The Curl port is never an oracle (ADR-0003).

## linux-x64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed. Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing. It supersedes run 44044472038 and the later scheduled run 45198867449 (2026-11-23).

- **This family:** 26 passed, 0 failed, 0 not executed.
- **Detail:** Dict 5, Gopher 3, Telnet 3, Tftp 6, Mqtt 9 passed.
- **Not classified here.** BL-1626 classifies the linux-x64 failures.

### Earlier run (44044472038, 2026-11-17)

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339, BL-1307) completed.
Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing.

Findings:

- **Cases compared.** The test job accepted the Curl port hash its own build job reproduced, so no
  `UnpinnedCurlPortClientException` was thrown (it replaces run 44012635963, where nothing compared).
- **This family:** 26 passed, 0 failed (Dict 5, Gopher 3, Telnet 3, Tftp 6, Mqtt 9).
- **Triaged by BL-1343.** No case of this family failed on linux-x64, so nothing is added to its findings table.
