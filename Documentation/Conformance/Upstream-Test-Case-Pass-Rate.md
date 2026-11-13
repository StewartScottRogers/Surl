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
- A platform not yet run says `not run`. Windows' row comes from a local run, since CI has no
  pinned Windows build (decision 6).

## Current

| Platform | Commit | Date | Reference pin SHA-256 | Total | Runnable | Passed | Failed | Skipped | test-server-choice | Pass rate |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `win-x64` | `153bbc14` + BL-549 + BL-855 + BL-896 + BL-897 + BL-898 + BL-899 + BL-900 + BL-556 + BL-682 + BL-562 + BL-689 + BL-776 + BL-856 | 2026-11-12 | `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778` | 2,013 | 1,142 | 1,129 | 13 | 871 | 34 | 98.9% |
| `linux-x64` | `8eb0301b` | 2026-11-06 | `153CA463957609117D21A848BE29B70691B85F9E5CC9370C7DAA037B839A4E45` | 2,013 | 1,203 | 1,154 | 49 | 810 | 34 | 95.9% |
| `osx-arm64` | `8eb0301b` | 2026-11-06 | `04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF` | 2,013 | 1,203 | 1,153 | 50 | 810 | 34 | 95.8% |

BL-856 (2026-11-12) added the 6 cases that fail only on a byte upstream's test server chose: MQTT
1190, 1192, 2204, 3018 and 1640 (RETAIN set on a new subscription's `PUBLISH`) and TFTP 283 (ERROR 1
for a missing file). None is on a passing list, so on every platform they move from **Failed** and
**Runnable** to **Skipped** and **test-server-choice** without a new run (the Windows numbers are
BL-776's run less these 6; Linux and macOS are BL-776's arithmetic less these 6).

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
| `debug-build` | 50 | 50 | 50 |
| `disabled-upstream` | 19 | 14 | 14 |
| `feature-missing:<name>` | 127 | 12 | 12 |
| `libtest` | 349 | 350 | 350 |
| `no-server-half` | 87 | 87 | 87 |
| `perl-command` | 27 | 27 | 27 |
| `postcheck` | 14 | 14 | 14 |
| `precheck` | 19 | 19 | 19 |
| `server-half-not-composed:<name>` | 13 | 15 | 15 |
| `server-not-built:<name>` | 38 | 67 | 67 |
| `servercmd-unsupported:<command>` | 30 | 35 | 35 |
| `strippart-unsupported` | 1 | 1 | 1 |
| `test-server-choice` | 34 | 34 | 34 |
| `unittest` | 81 | 81 | 81 |
| `unparsable` | 4 | 4 | 4 |
| **Total** | **871** | **810** | **810** |

## Releases

No release has been tagged yet. Before Stewart tags `v<version>`, the release commit adds one row
per platform here, copied from the current table; `.github/workflows/release.yml` puts the rows
for the tag's version at the top of the release notes and fails when there are none.

| Version | Platform | Commit | Reference pin SHA-256 | Runnable | Passed | Failed | Skipped | test-server-choice | Pass rate |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
