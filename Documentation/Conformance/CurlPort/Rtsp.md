# Phase 7 findings: RTSP

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, which contains BL-1622 and BL-1623; Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`) is the current outcome on linux-x64 and osx-arm64 and supersedes every earlier run named below. Both test jobs ran. This family: linux-x64 16 passed, 0 failed, 0 not executed; osx-arm64 0 passed, 0 failed, 16 not executed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work (BL-1625 classifies osx-arm64, BL-1626 linux-x64), not a CI finding.

What `CurlPortBesideUpstreamCurlOverRtspTests` in `Conformance/Surl.Conformance.UnitTests` found when it
ran every RTSP conversation of `UpstreamCurlTalksToSurlOverRtspTests` (the tool rows of ADR-0074
decision 11) through pinned upstream curl and the pinned Curl port, each against its own fresh `surl`
(ADR-0089, BL-588).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, `curl.exe` SHA-256 `7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build, `UpstreamCurlBuilds.json`) |
| Surl | commit `fdb0ab43767137d4ce308750d887b2fb2b2ab625` |
| Platform | Windows (`win-x64`); off Windows every case is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-05; re-run 2026-11-16 at port commit 74f3f45: every listed finding still holds, none fixed, none added |

## Result

16 cases on Windows: 13 agreements, 3 port defects, no Surl defect, none not comparable. Upstream
curl's run is what the mirrored test asserts in all 16.

Compared, per ADR-0089 decision 4: exit code, standard output (with `-i`'s `Date:` value normalised),
the `-v` request lines `Options_MissingPathWithFieldsVerbose_Exits0AndSendsOptionsStar` asserts on,
`surl`'s `-v` notes, and the bytes each client sent on its RTSP connection, with `User-Agent:` and
Digest's `cnonce` and `response` normalised. `Options_AnyAuth_Exits0WithDigest`'s mirrored test asserts on
the `> Authorization: Digest ` standard error line, whose `cnonce` and `response` differ every run; that
line is not compared as standard error, and the same header is compared in the request bytes instead.

## The libcurl rows

The rows of `PinnedLibcurlTalksToSurlOverRtspTests` (ADR-0074 Amendment 1) are not measured here. The
port's `Libcurl` is a managed API, not a native `libcurl-4.dll`, so ADR-0089 decision 1 (c) gives them
to BL-597, whose findings go in `Libcurl.md`.

## Findings

The test reads the first column of this table (ADR-0089 decision 5): a listed case must still show its
port defect, and an unlisted case must agree.

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |
| `Options_Digest_Exits0OnOneConnection` | `UpstreamCurlTalksToSurlOverRtspTests.Options_Digest_Exits0OnOneConnection` | RTSP-1 |
| `Options_DigestWrongPassword_Exits22With401` | `UpstreamCurlTalksToSurlOverRtspTests.Options_DigestWrongPassword_Exits22With401` | RTSP-1 |
| `Options_AnyAuth_Exits0WithDigest` | `UpstreamCurlTalksToSurlOverRtspTests.Options_AnyAuth_Exits0WithDigest` | RTSP-1 |

### RTSP-1: the port does not answer an RTSP Digest challenge

- **Phase 7 tests:** the three rows above, each mirroring the test of the same name in
  `UpstreamCurlTalksToSurlOverRtspTests`.
- **Command lines** (`{surl}` is `rtsp://127.0.0.1:<port>`; `surl rtsp://… --user tester:secret`):
  - `curl -sS -m 20 -f --digest -u tester:secret -w %{num_connects} {surl}/clip.bin`
  - `curl -sS -m 20 -f --digest -u tester:wrong -w %{response_code} {surl}/clip.bin`
  - `curl -sS -m 20 -v -f --anyauth -u tester:secret {surl}/clip.bin`
- **Upstream's result:** sends `OPTIONS * RTSP/1.0` without credentials, reads `surl`'s
  `401 Unauthorized` with its `WWW-Authenticate: Digest` challenge, then sends a second `OPTIONS *` on
  the same connection with `Authorization: Digest …`. With the right password it exits 0 (`-w
  %{num_connects}` writes `1`); with the wrong one `surl` answers 401 again and it exits 22 (`401`).
- **Port's result:** sends the first `OPTIONS * RTSP/1.0` only and stops. Request bytes on connection 0:
  upstream has byte `0x4F` (`O`, the second request's `OPTIONS`) at offset 57 of the normalised bytes;
  the port's bytes end at offset 57. With the right password the port exits 22 where upstream exits 0
  (`Options_Digest_Exits0OnOneConnection`, `Options_AnyAuth_Exits0WithDigest`); with the wrong one both
  exit 22, but the port never sent the Digest request upstream sends.
- **What is right:** RFC 2326 section 14 (`Authorization`, `WWW-Authenticate`) takes RTSP's
  authentication from HTTP, so the Digest exchange of RFC 7616 (section 3.4: the client answers a 401
  challenge with an `Authorization: Digest` request) applies. curl 8.21.0's `--digest` and `--anyauth`
  (`curl.1`) do the same over RTSP: `lib/rtsp.c` builds every request through `Curl_http_output_auth`
  and follows a 401 through the HTTP authentication negotiation, re-sending on the same connection.

## osx-arm64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed with the `Phase 7 families (osx-arm64)` job run. Its `phase-7-osx-arm64-results` artifact holds 489 results for all Phase 7 test classes.

- **This family:** 0 passed, 0 failed, 16 not executed.
- **Detail:** Not executed: 16, Inconclusive with "UpstreamCurlBuilds.json pins no upstream curl build for osx-arm64 that supports rtsp." The osx-arm64 reference build lists no `rtsp` protocol, so this is the locator's message, not a finding.
- **Cases compared.** The `UpstreamCurlBuilds.json` pin for `osx-arm64` (curl 8.21.0, SHA-256 `04E0E69B...EEBF`, pinned 2026-09-28) was found, and BL-1622 removed `UnpinnedCurlPortClientException` (357 of them in run 44044472038), so cases that reach the port compared it with upstream curl. An earlier statement that no upstream curl build is pinned for `osx-arm64` was false.
- **No finding is classified here.** Failures are recorded, not classified; BL-1625 classifies the osx-arm64 ones. The Curl port is never an oracle (ADR-0003).

## linux-x64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed. Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing. It supersedes run 44044472038 and the later scheduled run 45198867449 (2026-11-23).

- **This family:** 16 passed, 0 failed, 0 not executed.
- **Detail:** Every case passed.
- **Not classified here.** BL-1626 classifies the linux-x64 failures.

### Earlier run (44044472038, 2026-11-17)

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339, BL-1307) completed.
Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing.

Findings:

- **Cases compared.** The test job accepted the Curl port hash its own build job reproduced, so no
  `UnpinnedCurlPortClientException` was thrown (it replaces run 44012635963, where nothing compared).
- **This family:** 16 passed, 0 failed.
- **Triaged by BL-1343.** No case of this family failed on linux-x64, so nothing is added to its findings table.
