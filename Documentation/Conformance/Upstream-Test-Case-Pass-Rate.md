# Upstream test-case pass rate

How many of upstream curl's own test cases (tag `curl-8_21_0`, 2,013 cases) Surl passes when it
plays their server half with each platform's pinned **reference** build of curl 8.21.0 as the
client ([ADR-0080](../Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md),
decision 5). This is success criterion 2 of the
[product overview](../Product/Product-Overview.md): stated per release, and it only rises.

- **Runnable** is the cases that ran: every case not skipped. **Pass rate** is passed over runnable.
- **Skipped** counts every skip; the table below it gives each reason (decision 3's, plus
  `server-half-not-composed:<name>` for a server half the suite does not compose yet and
  `unparsable` for a case the parser refuses). A skip is never a pass.
- **test-server-choice** counts the cases excluded because the byte they verify is the upstream
  test server's arbitrary choice, listed in
  [Upstream-Test-Case-Exclusions.md](Upstream-Test-Case-Exclusions.md); they are counted on their
  own so they cannot hide among the skips.
- Beside this file, `upstream-test-cases-passing.<platform>.txt` holds the numbers of the cases
  that pass, one per line. The suite (`UpstreamTestCasesAgainstSurlTests`, `Integration` and
  `UpstreamTestCases`) fails, naming each case and its first differing section, when a listed case
  does not pass; a case that passes but is not listed is written to the test log with the lines to
  add. `UpstreamTestCasePassRateStatementTests`, a fast test, fails when a platform's **Passed**
  below differs from its list's line count, or when a platform's count falls from one releases
  row to the next.
- To raise a platform's count: take the run's `upstream-test-cases-passing.<platform>.txt` (beside
  its results file; CI's `Upstream test cases` job uploads both as the
  `upstream-test-case-results-<platform>` artifact), copy it over the one here, and update that
  platform's row. Removing a number from a list is allowed only together with an exclusions row
  for that case.
- A listed case that skips `debug-build`, `libtest` or `libtest-not-built` in a run that has no
  Debug build or no libtests binary (the release workflow's published-binary job has only the
  reference curl) is not a regression, since the run cannot play it ([ADR-0173](../Planning/Decisions/ADR-0173-a-debug-upstream-curl-8-21-0-build-from-source-for-the-http-2-and-http-3-test-cases-that-need-one.md),
  amendment 1). It stays a skip in the results and is never counted as passed; where the binary
  is present, a skip of a listed case is still a regression.
- A platform not yet run says `not run`. Windows' row comes from a local run, since CI has no
  pinned Windows build (decision 6).

## Current

| Platform | Commit | Date | Reference pin SHA-256 | Total | Runnable | Passed | Failed | Skipped | test-server-choice | Pass rate |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `win-x64` | `c3011bee` | 2026-11-14 | `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778` | 2,013 | 1,185 | 1,185 | 0 | 828 | 34 | 100.0% |
| `linux-x64` | `4ac7b2c7` | 2026-11-17 | `153CA463957609117D21A848BE29B70691B85F9E5CC9370C7DAA037B839A4E45` | 2,013 | 1,326 | 1,326 | 0 | 687 | 34 | 100.0% |
| `osx-arm64` | `145f16b2` | 2026-11-14 | `04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF` | 2,013 | 1,316 | 1,316 | 0 | 697 | 34 | 100.0% |

BL-1317 (2026-11-16) moved Linux to commit `6023b4b3` from [CI run 43979679184](https://github.com/StewartScottRogers/Surl/actions/runs/43979679184), the first Linux run with the Debug build BL-1311 selects: 2400, 2406, 358 and 359 pass; 2401 and 2501 differ in the `Via` header nghttpx adds (BL-1321); 2402, 2404 and 2502 record nothing (BL-1322); 2407 has curl exit 6 (BL-1323). No case that passed before fails. macOS keeps its row until its Debug build runs.

BL-1321 (2026-11-16) made the gateway's forwarded request carry the `Via` nghttpx writes (`2 nghttpx` for HTTP/2, `3 nghttpx` for HTTP/3, the case 2401 and 2501 `<protocol>` bytes) and dropped `X-Forwarded-Proto` from the HTTP/3 request (case 2501 shows none). 2401 and 2501 are in the `linux-x64` passing list from the unit tests that replay those bytes; the Linux row counts them as passing (1,322 of 1,326) until the next CI run measures them.

BL-1325 (2026-11-16) moved Linux to commit `0cd51f95` from [CI run 44002670831](https://github.com/StewartScottRogers/Surl/actions/runs/44002670831), the first Linux run with the BL-1322 fix (`2df5bfc9f`): 2402, 2404 and 2502 now pass and are in `upstream-test-cases-passing.linux-x64.txt` (1,325 passed). Only 2407 still fails on Linux, and it no longer has curl exit 6 (BL-1323): the run records `test2407 fail timeout`.

BL-1333 (2026-11-16) looked for the cause of that timeout. Case 2407 runs `lib2405` (`curl_multi_waitfds` over HTTP/2) and the runner stops it at its timeout, but the results artifact keeps only the verdict, not the stdout and stderr the client wrote before it was stopped, so the cause is not yet known and is not known to be a Surl defect. BL-1334 made the artifact carry that output.

BL-1335 (2026-11-16) read the `test2407` block of `upstream-test-case-timeout-output.linux-x64.txt` from [CI run 44009482486](https://github.com/StewartScottRogers/Surl/actions/runs/44009482486). Cause: a Surl defect in the `http/2` gateway half. `lib2405` opens two connections to the gateway's plaintext port; each sends `GET /2407` with `Upgrade: h2c`, each is answered `101 Switching Protocols`, and curl logs `Received 101, Switching to HTTP/2` and `Copied HTTP/2 data in stream buffer ... len=21` (the server's SETTINGS frames). Then no response to stream 1 arrives: stdout is 0 bytes and the runner stops curl at 60 s. The upgraded connections therefore never write HEADERS and DATA for stream 1 (`Http2GatewayCleartextUpgrade`, `Http2ConnectionServer.ServeUpgradedAsync`, `Http2GatewayStreamForwarder`). BL-1336 fixes it.
BL-1326 (2026-11-17) moved Linux to commit `4ac7b2c7` from [CI run 44068385591](https://github.com/StewartScottRogers/Surl/actions/runs/44068385591), the first Linux run with the BL-1336 fix (`b2ff44414`): 2407 passes and is in `upstream-test-cases-passing.linux-x64.txt` (1,326 passed, none failed).

[ADR-0173](../Planning/Decisions/ADR-0173-a-debug-upstream-curl-8-21-0-build-from-source-for-the-http-2-and-http-3-test-cases-that-need-one.md)
(BL-1312) is why `debug-build` falls from 50 to 44 on `linux-x64`: the Linux job builds a Debug curl
and the suite plays the six cases that need it (2400, 2401, 2406, 2501, 358 and 359) with that
build. Windows and macOS stay at 50 because neither has a reproducible Debug build (Docker Desktop
yields Linux binaries, and there is no macOS runner for one), so their rows and counts do not move.
The four libtest cases (2402, 2404, 2407 and 2502) were skipped under ADR-0173's own build, which
has no libtest program; ADR-0174 (BL-1313) added the runner for `<tool>` programs and BL-1316 its
code. Since the Linux run at `6023b4b3` they run there with the Debug libtests binary and no longer
count under `libtest` or `debug-build` on `linux-x64`. In that run all four failed and were recorded
as failures with their reasons, never excluded (ADR-0174 decision 6): 2402, 2404 and 2502 recorded
nothing (BL-1322) and 2407 had curl exit 6 (BL-1323). All four pass on `linux-x64` now: 2402, 2404
and 2502 from commit `0cd51f95` (BL-1325) and 2407 from commit `4ac7b2c7` (BL-1326). `win-x64` and
`osx-arm64` still skip them.

BL-1149 (2026-11-14) moved Linux and macOS to commit `145f16b2`, from [CI run 43651751537](https://github.com/StewartScottRogers/Surl/actions/runs/43651751537) (PR #36): 956, 957 and 958 now pass on both (`RCPT` and `VRFY` answer `501` for a non-ASCII address unless the case lists `SMTPUTF8`, as upstream `ftpserver.pl` does), so each is 1,316 passed, 0 failed and both failures files are empty. The paragraph below is the earlier BL-1142 record, kept as history.

BL-1142 (2026-11-14) recorded all three rows at commit `c3011bee`, which contains BL-1126 to BL-1134, BL-1136,
BL-1137, BL-1139, BL-1140, BL-1141 and BL-1143 to BL-1147. Linux and macOS come from CI run
[43650373643](https://github.com/StewartScottRogers/Surl/actions/runs/43650373643) (`workflow_dispatch` on
`factory/phase-1`): 1,316 runnable, 1,313 passed, 3 failed, 697 skipped on each, and the two results files are
identical, so 4001 now passes on macOS too. The Windows row is a local run of the same commit (1,185 runnable, all
passed, 828 skipped; 1452 passes). Still failing on Linux and macOS: 956 (`protocol`, curl sent `DATA` where the case
expects `QUIT`), 957 and 958 (`errorcode`, curl exits 0, the case expects 8); BL-1127 named them and a follow-up task
holds them. Every other case named by the tasks above passes, and no `servercmd-unsupported` or
`server-half-not-composed` skip is left on any platform. The same CI run's macOS fast-test leg failed
`UpgradeToTlsAsync_TransportResetDuringHandshake_ThrowsTlsHandshakeException` (an `IOException` where
`TlsHandshakeException` was expected), which is not part of this measurement.

BL-1121 (2026-11-14) recorded the Linux and macOS rows from CI run
[43629075978](https://github.com/StewartScottRogers/Surl/actions/runs/43629075978) (`workflow_dispatch` on
`factory/phase-1`, head `356da7cd`, which contains BL-1117 to BL-1120): the first with the http/2, http/3 and
mail-TLS server halves composed. Linux is 1,281 runnable, 1,259 passed, 22 failed, 732 skipped; macOS is 1,281
runnable, 1,258 passed, 23 failed, 732 skipped (4001 fails `errorcode` on macOS only, as before). Cases 1700, 1701,
1702, 2403, 2500, 2503, 987, 988 and 989 all pass on both legs. No `server-not-built` skip is left; the two
`server-half-not-composed` skips are `https-mtls`. The failures are 165, 955-958, 962-968 and 1800 (`protocol`),
311, 312, 1072 and 1073 (`errorcode`), 469 (`stderr`), 1448, 2046 and 2047 (`stdout`) and 1452 (`timeout`).

BL-1120 (2026-11-14) composed the `imaps`, `pop3s` and `smtps` server halves: the IMAP, POP3 and SMTP case servers
also listen behind implicit TLS on `127.0.0.1`, and `%IMAPSPORT`, `%POP3SPORT` and `%SMTPSPORT` are their ports. Cases
987, 988 and 989 no longer skip `server-half-not-composed` and pass on the Windows reference build (1,155 runnable,
1,154 passed, 1 failed, 858 skipped); no case that passed fails. Linux and macOS rows come from BL-1121's CI run, below.

BL-1072 (2026-11-12) composed the `http-ipv6` server half: the plaintext HTTP half also binds `[::1]` and
`%HTTP6PORT` is its port. The 10 cases that named it (1230 among them) no longer skip
`server-half-not-composed:http-ipv6`; the Windows run is 1,152 runnable, 1,144 passed, 8 failed, 861 skipped, and 14
cases newly pass (30, 37, 240, 242, 263, 1046, 1230, 1265, 1324, 1408, 1456, 2002, 2003, 3202). Linux and macOS keep
their numbers until their next CI run.

BL-856 (2026-11-12) added the 6 cases that fail only on a byte upstream's test server chose: MQTT
1190, 1192, 2204, 3018 and 1640 (RETAIN set on a new subscription's `PUBLISH`) and TFTP 283 (ERROR 1
for a missing file). None is on a passing list, so on every platform they move from **Failed** and
**Runnable** to **Skipped** and **test-server-choice** without a new run (the Windows numbers are
BL-776's run less these 6; Linux and macOS are BL-776's arithmetic less these 6).

BL-1071 (2026-11-12) found why 1297 timed out: its origin is the HTTP half, whose `<data>` part is empty, and
BL-1078 had made that half hold the connection open on an empty part (for 1548's raw HTTP/1.0 telnet probe);
upstream's `sws` closes after a zero-byte reply, so curl, waiting through the tunnel, never saw the close. The HTTP
half now closes after an empty part on an HTTP/1.1 request and holds it open on HTTP/1.0 (ADR-0152). A local run of
1297 and 1548 passes both; 1297 is on the Windows list (1,130 passed, 12 failed); the other rows keep their numbers
until their next CI run.

BL-776 (2026-11-12) added the 24 FTP cases that verify upstream's greeting banner (1349-1354,
1357-1362, 1379-1384, 1387-1392) to the exclusions file. The Windows row is a local run: 1,148
runnable, 1,129 passed, 19 failed, 865 skipped, 28 `test-server-choice`. Linux and macOS lose the
same 24 from **Failed**, **Runnable** and add them to **Skipped** without a new run (they failed
`file2` there too). Test 1847 failed `protocol` in one of three local runs and passed in the
other two, so it is intermittent.

BL-689 (2026-11-12) made the selection rules read the exclusions file's 4 rows (802, 818, 861,
897). On every reference pin each of them reaches decision 5's check
(`UpstreamTestCaseCorpusTests`) and none is on a passing list, so each was one of the runs'
failures below; the rows move them from **Failed** and **Runnable** to **Skipped** and
**test-server-choice** without a new run. The failure counts quoted below are the runs' own,
those 4 included.

Linux and macOS: CI run
[42203619501](https://github.com/StewartScottRogers/Surl/actions/runs/42203619501) (`master`,
commit `8eb0301b`), whose `upstream-test-case-results-<platform>` artifacts hold each leg's
results file and passing list now that BL-393 fixed their upload path. The two legs agree on every case but 4001, which passes on Linux
and fails `errorcode` on macOS. Both skip fewer cases than Windows, chiefly `feature-missing`
(127 on Windows, 12 here: their reference builds have features the Windows one lacks). Of the
83 Linux failures 31 differ in `protocol`, 24 in `file2` (upstream's test-server banner, BL-776),
8 in `data`, 7 in `errorcode`, 4 each in `stdout` and `timeout`, 3 in `stderr` and 1 each in
`file` and `upload`.

Windows: the run of BL-562's changes, the first with the `socks4`, `socks5` and `socks5unix` server halves
composed (`SocksUpstreamTestCaseServer` on `%SOCKSPORT`, a SOCKS4 and a SOCKS5 listener, and on `%SOCKSUNIXPATH`;
every request goes to `127.0.0.1`, or to the case's `backendport`, as upstream's `socksd` does). All 22 cases that
run on Windows pass: 700, 701, 702, 703, 706, 707, 708, 709, 710, 711, 712, 713, 715, 716, 717, 719, 720, 721, 728, 729,
742 and 2055 (715 and 2055 now that BL-556 built `http-proxy`). 1467, 1468 and 1470 skip `feature-missing:!win32`
and run on the Linux and macOS pins at their next CI run. The same run found 1548 failing `errorcode` (curl exits 0,
the case expects 28): BL-683 made the HTTP half close a connection whose reply part is empty, which 1548 relied on
being held open - not a SOCKS change; its number stays in the list.

The run before, of BL-556's changes, the first with the `http-proxy` and `https-proxy` server halves
composed (`HttpProxyUpstreamTestCaseServer` on `%PROXYPORT`, and on `%HTTPSPROXYPORT` behind TLS; every
tunnel goes to `127.0.0.1` at the `CONNECT`'s port, as upstream's `sws` sends it). Of the 29 cases that
named them, 25 pass (80, 83, 95, 275, 445, 714, 744, 1078, 1287, 1288, 1319, 1320, 1321, 1428, 1586, 1630,
1631, 1632, 1849, 1904, 2050, 2107, 3028, 3106, 3401), 1297 fails `timeout`, 1230 skips
`server-half-not-composed:http-ipv6`, and 715 and 2055 keep `server-not-built:socks5`. No case that passed
fails. Linux and macOS keep their rows above until their next CI run.

The run before, of BL-898's changes: a case naming several servers is no longer skipped for a
`<servercmd>` word that one of them implements (cases 779 and 795, `http` then `imap`, `AUTH`), and each
server half gets the lines its own row implements. Both pass; none that passed fails.

The run before, of BL-549's changes on top of commit `153bbc14`, the first with the `http-unix`
server half composed: the plaintext HTTP server half also listens on a Unix domain socket file in
a short folder of its own under the temporary folder, named to curl as `%HTTPUNIXPATH`. Both
`http-unix` cases, 1435 and 1436, pass, and none that passed fails; `server-not-built` falls by
those 2. Linux and macOS keep `server-not-built:http-unix` in their row above until their next CI
run, which covers both cases on their reference pins (each reports `UnixSockets`).

The run before, of BL-681's changes on top of commit `5ac86cca`, was the one where the HTTP server half reads
past a PROXY protocol version 1 header before the first request and records it as the first
received line. 3 more cases pass - 1455, 3201 and 3220 - and none that passed fails. The skips are
unchanged.

The run before, of BL-680's changes on top of commit `f20ac200`, was the one where the HTTP server half takes
the reply part from the digits right after the request target's last `/`, query included
(`/data/10240002.txt` names part 2). 19 more cases pass - 11, 28, 40, 43, 187, 276, 309, 1024,
1025, 1031, 1051, 1052, 1053, 1064, 1065, 1067, 1138, 1168 and 2081 - and none that passed
fails. The skips are unchanged.

The run before, of BL-879's changes on top of commit `53a7920c`, was the first with the `imap`,
`pop3` and `smtp` server halves composed (plaintext, on `127.0.0.1` and `[::1]`). The mail family -
every case naming `imap`, `pop3` or `smtp`, 216 at the tag - ran 160: 146 pass, 14 fail (BL-879's
Notes group them; BL-899 to BL-897 and the exclusions file take them), and 56 skip. `imaps`,
`pop3s` and `smtps` (one case each) still skip `server-half-not-composed`. Every other family is as
BL-397 left it.

The run before that, of BL-397's changes on top of commit `f83e4c79`, was the first with the `scp`, `sftp`
and `smb` server halves composed: Surl's own SSH and SMB servers serving the case's files. The
`smb` cases run with the supplementary build ADR-0030 pinned for SMB, since the reference build has
no `smb`. Of the new servers: SFTP 24 ran, 24 pass; SCP 14 ran, 14 pass; SMB 2 ran, 2 pass; and
2004, which uses TFTP, SFTP and SCP in turn, passes. The other 20 cases naming `scp` or `sftp` skip:
14 run a Perl `<precheck>` (9) or `<postcheck>` (5), 2 are libtests, 2 unit tests, 1 needs an HTTP
proxy (445), and 632 names a port nothing listens on, so it needs no server half. Every other family is as
BL-396 left it: HTTP 686 ran (651 pass), FTP 202 ran (178 pass; the 24 that fail each verify
upstream's test-server greeting banner in a `-D` file, BL-776), TFTP 15 ran, 13 pass (2002 and
2003, which use HTTP, FTP and TFTP in turn, fail); MQTT 11 ran, 6 pass; Gopher 5 ran, 5 pass; DICT
1 ran, 1 pass; TELNET 1 ran, 0 pass. BL-396's Notes group those failures and name their tasks.

### Skipped, by reason

| Reason | `win-x64` | `linux-x64` | `osx-arm64` |
| --- | ---: | ---: | ---: |
| `debug-build` | 50 | 44 | 50 |
| `disabled-upstream` | 19 | 14 | 14 |
| `feature-missing:<name>` | 143 | 16 | 16 |
| `libtest` | 349 | 346 | 350 |
| `no-server-half` | 87 | 87 | 87 |
| `perl-command` | 27 | 27 | 27 |
| `postcheck` | 14 | 14 | 14 |
| `precheck` | 19 | 19 | 19 |
| `server-half-not-composed:<name>` | 0 | 0 | 0 |
| `server-not-built:<name>` | 0 | 0 | 0 |
| `servercmd-unsupported:<command>` | 0 | 0 | 0 |
| `strippart-unsupported` | 1 | 1 | 1 |
| `test-server-choice` | 34 | 34 | 34 |
| `unittest` | 81 | 81 | 81 |
| `unparsable` | 4 | 4 | 4 |
| **Total** | **828** | **687** | **697** |

## Releases

Before Stewart tags `v<version>`, the release commit adds one row per platform here, copied from
the current table; `.github/workflows/release.yml` puts the rows for the tag's version at the top
of the release notes and fails when there are none. `0.1.0` is the first release (Stewart,
2026-11-16).

| Version | Platform | Commit | Reference pin SHA-256 | Runnable | Passed | Failed | Skipped | test-server-choice | Pass rate |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `0.1.0` | `win-x64` | `c3011bee` | `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778` | 1,185 | 1,185 | 0 | 828 | 34 | 100.0% |
| `0.1.0` | `linux-x64` | `145f16b2` | `153CA463957609117D21A848BE29B70691B85F9E5CC9370C7DAA037B839A4E45` | 1,316 | 1,316 | 0 | 697 | 34 | 100.0% |
| `0.1.0` | `osx-arm64` | `145f16b2` | `04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF` | 1,316 | 1,316 | 0 | 697 | 34 | 100.0% |
| `0.1.1` | `win-x64` | `c3011bee` | `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778` | 1,185 | 1,185 | 0 | 828 | 34 | 100.0% |
| `0.1.1` | `linux-x64` | `4ac7b2c7` | `153CA463957609117D21A848BE29B70691B85F9E5CC9370C7DAA037B839A4E45` | 1,326 | 1,326 | 0 | 687 | 34 | 100.0% |
| `0.1.1` | `osx-arm64` | `145f16b2` | `04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF` | 1,316 | 1,316 | 0 | 697 | 34 | 100.0% |
| `0.1.2` | `win-x64` | `c3011bee` | `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778` | 1,185 | 1,185 | 0 | 828 | 34 | 100.0% |
| `0.1.2` | `linux-x64` | `4ac7b2c7` | `153CA463957609117D21A848BE29B70691B85F9E5CC9370C7DAA037B839A4E45` | 1,326 | 1,326 | 0 | 687 | 34 | 100.0% |
| `0.1.2` | `osx-arm64` | `145f16b2` | `04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF` | 1,316 | 1,316 | 0 | 697 | 34 | 100.0% |

### Why 1072, 1073 and 4001 fail on CI (2026-11-14)

[CI run 43637240745](https://github.com/StewartScottRogers/Surl/actions/runs/43637240745) (head `ca55e264`) shows
why three cases fail off Windows. Cases 1072 and 1073 (`linux-x64`, `osx-arm64`): curl exits 65
(`seek callback returned error 2`), not 25, because the runner gives curl a pipe on stdin and curl cannot
rewind it after the 401; fix in BL-1143. Case 4001 (`osx-arm64` only): curl exits 35 (`tlsv1 alert protocol
version`), not 101, because ECH needs TLS 1.3 and .NET's macOS server `SslStream` has none; ADR task BL-1144.
