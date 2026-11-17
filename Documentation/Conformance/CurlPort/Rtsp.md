# Phase 7 findiRun [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7` (2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339) is the current linux-x64 outcome and supersedes the run below. Nothing threw `UnpinnedCurlPortClientException`: the test job accepted the hash its own build job reproduced (BL-1340, BL-1339), so cases compared pinned upstream curl with the Curl port. This family: 16 passed, 0 failed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work, not a CI finding.

ngs: RTSP

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

Run [44012635963](https://github.com/StewartScottRogers/Surl/actions/runs/44012635963) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `a6a289ce2e2d9691c1dd46894f92002bd84ce249`, Curl port commit
`74f3f45c47e86300b2b262dc1dacd43b508b09aa`) completed; its `Phase 7 families and OpenLdap_ cases (linux-x64)` job
ran green because the test step is `continue-on-error` (BL-1307). Its `phase-7-linux-x64-results` artifact holds 489
results for all Phase 7 test classes: 1 passed, 441 failed, 47 not executed.

Findings:

- **Nothing was compared.** All 441 failures are the same `UnpinnedCurlPortClientException`: the port published in the
  test job hashed `048AAEE01EAD37D136AF842303E8C90438B1E193DEDC0654F3CFCA4A1E555364`, but `CurlPortBuilds.json` pins
  `DF507F32E8B3E5F0444F89544A62C18C4CFA96C838AF8C98D74B9B044F8CF57A`, which the `Build the Curl port (linux-x64)` job of
  the same run reproduced twice. The port was published before any case ran, so the difference comes from the test
  job, not from the port. It is a CI finding, not a port defect and not a Surl defect.
- **Not executed:** 23 Windows-only, 2 macOS-only, and 15 `Assert.Inconclusive` ("pins no supplementary upstream curl
  build for linux-x64") cases, plus the Kerberos/GSSAPI cases.
- **No port defect and no Surl defect is recorded for linux-x64.** The family's findings table is unchanged.
