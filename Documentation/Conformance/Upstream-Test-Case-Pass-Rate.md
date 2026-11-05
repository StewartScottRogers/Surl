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
| `win-x64` | `6417e341` + BL-396 | 2026-11-05 | `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778` | 2,013 | 923 | 854 | 69 | 1,090 | 0 | 92.5% |
| `linux-x64` | not run | not run | `153CA463957609117D21A848BE29B70691B85F9E5CC9370C7DAA037B839A4E45` | 2,013 | not run | not run | not run | not run | not run | not run |
| `osx-arm64` | not run | not run | `04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF` | 2,013 | not run | not run | not run | not run | not run | not run |

Linux and macOS: CI run
[41980108989](https://github.com/StewartScottRogers/Surl/actions/runs/41980108989) (commit
`b1a280aa`) ran the suite on both and it passed, but uploaded no results file, so their counts are
not known yet; BL-393 fixed the upload, and BL-688 fills both rows from the next run.

Windows: the run of BL-396's changes on top of commit `6417e341`, the first with the `dict`,
`gopher`, `gopher-ipv6`, `gophers`, `mqtt`, `mqtts`, `telnet` and `tftp` server halves composed
(every `rtsp` case is a libtest, so none runs). The HTTP and FTP families are as BL-394 left them:
686 ran (651 pass) and 202 ran (178 pass; the 24 that fail each verify upstream's test-server
greeting banner in a `-D` file, BL-776). Of the new servers: TFTP 15 ran, 13 pass, and the two
cases that use HTTP, FTP and TFTP in turn (2002, 2003) fail; MQTT 11 ran, 6 pass; Gopher 5 ran,
5 pass; DICT 1 ran, 1 pass; TELNET 1 ran, 0 pass. BL-396's Notes group the
failures and name their tasks.

### Skipped, by reason

| Reason | `win-x64` |
| --- | ---: |
| `debug-build` | 50 |
| `disabled-upstream` | 19 |
| `feature-missing:<name>` | 127 |
| `libtest` | 349 |
| `no-server-half` | 87 |
| `perl-command` | 27 |
| `postcheck` | 14 |
| `precheck` | 19 |
| `protocol-missing:<scheme>` | 2 |
| `server-half-not-composed:<name>` | 125 |
| `server-not-built:<name>` | 67 |
| `servercmd-unsupported:<command>` | 118 |
| `strippart-unsupported` | 1 |
| `unittest` | 81 |
| `unparsable` | 4 |
| **Total** | **1,090** |

## Releases

No release has been tagged yet. Before Stewart tags `v<version>`, the release commit adds one row
per platform here, copied from the current table; `.github/workflows/release.yml` puts the rows
for the tag's version at the top of the release notes and fails when there are none.

| Version | Platform | Commit | Reference pin SHA-256 | Runnable | Passed | Failed | Skipped | test-server-choice | Pass rate |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
