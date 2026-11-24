# Phase 7 findings: upstream curl's own test cases

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, which contains BL-1622 and BL-1623; Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`) is the current outcome on linux-x64 and osx-arm64 and supersedes every earlier run named below. Both test jobs ran. This family: linux-x64 1 passed, 0 failed, 0 not executed; osx-arm64 0 passed, 1 failed, 0 not executed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work (BL-1625 classifies osx-arm64, BL-1626 linux-x64), not a CI finding.

What `CurlPortBesideUpstreamCurlOverUpstreamTestCasesTests` in `Conformance/Surl.Conformance.UnitTests`
found when it played every case on `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt` -
the upstream test cases (tag `curl-8_21_0`) Surl's runner passes with pinned upstream curl as the client
(ADR-0080, BL-392 to BL-397 and BL-879) - twice: with the paired pinned upstream build and with the pinned
Curl port, each run against its own fresh server halves (ADR-0089 decision 1(b), amendment 7; BL-591).
Each case's own `<verify>` section judges both runs: it was written for upstream curl, so a port run
that fails it while upstream's passes is a port finding, never a Surl change (ADR-0003 decision 6).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, `curl.exe` SHA-256 `7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467` (`CurlPortBuilds.json`, `win-x64`); its `--version` says `curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel` |
| Upstream curl | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build, paired with every case but the smb ones) |
| Upstream curl, smb | SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0` (the supplementary build `LocateForProtocol` pairs with `smb`) |
| Surl | commit `7bf55f26a8eecda5f936db7e32f596770ee7adf5` with BL-591's changes |
| Platform | Windows (`win-x64`); off Windows the test is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-06; re-run 2026-11-16 at port commit 74f3f45: one finding added (UTC-4), none fixed |

## Result

1,185 cases on Windows: upstream passed all 1,185 (no Surl defect), the port passed 1,171, 12 are port
defects and 2 are not comparable. Since the last measurement (port `b42c319`, 1,041 cases) the passing list
grew by 144 cases and the new port commit `74f3f45` added one port defect, UTC-4; it fixed none of the
earlier eleven (each listed case still shows its defect).

| Family | Cases | Upstream passed | Port passed | Port defects | Not comparable |
| --- | ---: | ---: | ---: | ---: | ---: |
| dict | 1 | 1 | 1 | 0 | 0 |
| ftp | 190 | 190 | 188 | 2 | 0 |
| gopher | 5 | 5 | 5 | 0 | 0 |
| http | 747 | 747 | 742 | 5 | 0 |
| http-proxy | 2 | 2 | 2 | 0 | 0 |
| mail (imap, pop3, smtp) | 163 | 163 | 158 | 5 | 0 |
| mqtt | 16 | 16 | 16 | 0 | 0 |
| smb | 2 | 2 | 0 | 0 | 2 |
| socks | 5 | 5 | 5 | 0 | 0 |
| ssh (scp, sftp) | 38 | 38 | 38 | 0 | 0 |
| telnet | 1 | 1 | 1 | 0 | 0 |
| tftp | 15 | 15 | 15 | 0 | 0 |
| **All** | **1,185** | **1,185** | **1,171** | **12** | **2** |
A case's family is its first `<server>`'s. 

How the port ran each case (amendment 7):

- The port is described by its own `--version`, as runtests describes the curl it tests: `%VERSION` is
  `8.22.0` and `%VERNUM` `8.22.0` for the port's runs, `8.21.0` for upstream's; `%CURL` is each client's
  own path; the `%if` features and selection come from each client's own `Protocols:` and `Features:`
  lines. So a `User-Agent: curl/%VERSION` line is matched against each client's own version, as upstream's
  suite would match it.
- **Not comparable: the port's build lacks the case's protocol.** Tests 1451 and 1850 (`smb`): the port's
  `Protocols:` line has no `smb`, so runtests would skip both for it (`protocol-missing:smb`), as it skips
  them for the Windows reference build, which lacks `smb` too.
- Nothing of decision 4's normalisation applies: the case's `<verify>` section, with upstream's own
  strip lines, is the comparison, applied identically to both runs.

## Findings

The test reads the first column of this table (ADR-0089 decision 5): a listed case must still show its
port defect, and an unlisted case upstream passes must pass with the port too.

| Case | Family | Port's first failing section | Finding |
| --- | --- | --- | --- |
| `test251` | ftp | timeout | [UTC-4](#utc-4-active-mode-ftp-with-p-and-a-slow-server-does-not-finish) |
| `test798` | http | `file` (`log/jar798.txt`) | [UTC-1](#utc-1-a-cookie-with-a-domain-attribute-equal-to-the-host-is-saved-as-host-only) |
| `test838` | mail | timeout | [UTC-2](#utc-2-an-empty-sasl-external-response-is-sent-as-an-empty-line-not-) |
| `test884` | mail | `protocol` | [UTC-2](#utc-2-an-empty-sasl-external-response-is-sent-as-an-empty-line-not-) |
| `test943` | mail | `protocol` | [UTC-2](#utc-2-an-empty-sasl-external-response-is-sent-as-an-empty-line-not-) |
| `test1136` | http | `file` (`log/jar1136.txt`) | [UTC-1](#utc-1-a-cookie-with-a-domain-attribute-equal-to-the-host-is-saved-as-host-only) |
| `test1402` | http | `file` (`log/test1402.c`) | [UTC-3](#utc-3---libcurl-writes-its-options-in-another-order) |
| `test1404` | http | `file` (`log/test1404.c`) | [UTC-3](#utc-3---libcurl-writes-its-options-in-another-order) |
| `test1405` | ftp | `file` (`log/test1405.c`) | [UTC-3](#utc-3---libcurl-writes-its-options-in-another-order) |
| `test1406` | mail | `file` (`log/test1406.c`) | [UTC-3](#utc-3---libcurl-writes-its-options-in-another-order) |
| `test1407` | mail | `file` (`log/test1407.c`) | [UTC-3](#utc-3---libcurl-writes-its-options-in-another-order) |
| `test1465` | http | `file` (`log/test1465.c`) | [UTC-3](#utc-3---libcurl-writes-its-options-in-another-order) |

### Findings (linux-x64)

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) (BL-1343), played against the
linux-x64 passing list: two port defects that show on linux-x64 only (a heading that names a platform in
parentheses applies on that platform alone), and nine cases upstream skipped, which the test no longer judges (below).

| Case | Family | Port's first failing section | Finding |
| --- | --- | --- | --- |
| `test1072` | http | `errorcode` (port exited 65, expected 25) | [UTC-5](#utc-5-the-linux-x64-port-exits-65-where-upstream-exits-25-on-a-chunked-put-to-an-http10-server) |
| `test1073` | http | `errorcode` (port exited 65, expected 25) | [UTC-5](#utc-5-the-linux-x64-port-exits-65-where-upstream-exits-25-on-a-chunked-put-to-an-http10-server) |

### Findings (osx-arm64)

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) (BL-1624, triaged by BL-1625): the
same two port defects show on osx-arm64, where the port exits 65 and upstream (the reference build `04E0E69B…EEBF`)
exits 25. Every other failing case on osx-arm64 is already in the table above.

| Case | Family | Port's first failing section | Finding |
| --- | --- | --- | --- |
| `test1072` | http | `errorcode` (port exited 65, expected 25) | [UTC-5](#utc-5-the-linux-x64-port-exits-65-where-upstream-exits-25-on-a-chunked-put-to-an-http10-server) |
| `test1073` | http | `errorcode` (port exited 65, expected 25) | [UTC-5](#utc-5-the-linux-x64-port-exits-65-where-upstream-exits-25-on-a-chunked-put-to-an-http10-server) |

**Cases upstream skipped are not compared.** The linux-x64 passing list names test358, 359, 2400, 2401, 2402,
2404, 2406, 2501 and 2502, which need a Debug build of curl or the libtests binary. The Phase 7 test ran upstream
without either, so they ended `skip debug-build` or `skip libtest` and the comparison called each a Surl defect.
That was a test defect: the test now locates the supplementary Debug build and libtests binary as
`UpstreamTestCasesAgainstSurlTests` does, and leaves out a case upstream skips for want of the one it lacks
(`ToleratedSkipReasons`). The row count the test asserts still counts them.

#### UTC-5: the linux-x64 port exits 65 where upstream exits 25 on a chunked PUT to an HTTP/1.0 server

- **Cases:** test1072 "HTTP chunked PUT to HTTP 1.0 server with authorization"
  (`http://%HOSTIP:%HTTPPORT/1072 -T - -u testuser:testpass --anyauth`) and test1073 "HTTP chunked PUT to HTTP 1.0
  server with redirect" (`http://%HOSTIP:%HTTPPORT/1073 -T - -L`).
- **Upstream:** exit 25 (upload failed), as each case's `<errorcode>` says. **Port:** exit 65 in both.
- **Classification:** port defect (the port's linux-x64 and osx-arm64 builds, run 45412760425 for the latter: both pass on Windows). The cause inside the
  port is not traced.

Each case is run as runtests runs it (`UpstreamTestCaseCommandLine`): `-q`, `--output log/curl<N>.out`,
`--include`, `--trace-ascii log/trace<N>` and `--trace-time` as the case asks, then the case's
`<command>`. Below, the command lines are the cases' own, with `%HOSTIP` `127.0.0.1` and each port
variable an ephemeral port. Upstream's result is "the case passed": exit code, output, protocol bytes and
files all as the case's `<verify>` says.

### UTC-1: a cookie with a domain attribute equal to the host is saved as host-only

- **Cases:** test798 "HTTP cookies in a folded header" and test1136 "Check cookies against PSL".
- **Command lines:** `curl http://localhost:<port>/we/want/798 -b none -c log/jar798.txt`;
  `curl http://www.example.ck/1136 http://www.ck/1136 http://z-1.compute-1.amazonaws.com/1136 -b none -c log/jar1136.txt -x 127.0.0.1:<port>`.
- **What the server sends:** test798 `Set-Cookie: flavor=tasty;` folded onto ` domain=localhost;` and
  ` path=/p4/;`; test1136 `Set-Cookie: test5=forbidden5; domain=z-1.compute-1.amazonaws.com; path=/;`
  for the host `z-1.compute-1.amazonaws.com`.
- **Upstream:** the jar's cookie line is `#HttpOnly_.localhost	TRUE	/p4	TRUE	0	flavor	tasty`
  (test798) and `.z-1.compute-1.amazonaws.com	TRUE	/	FALSE	0	test5	forbidden5` (test1136): the
  domain with a leading dot and the include-subdomains flag `TRUE`.
- **Port:** the same cookies are written `#HttpOnly_localhost	FALSE	/p4	…` and
  `z-1.compute-1.amazonaws.com	FALSE	/	…`: no leading dot, flag `FALSE`. Line 5 of each jar is the
  first that differs; the sections the checker reads before `file` (`stdout`, `protocol`, the reply's
  `data`) pass.
- **What is right:** RFC 6265 section 5.3 step 6: a cookie whose `Domain` attribute is non-empty and
  domain-matches the host is a domain cookie (host-only-flag false), even when the attribute equals
  the host; only a cookie without the attribute is host-only. Upstream's `lib/cookie.c` at 8.21.0 sets
  `tailmatch` whenever a `domain=` is accepted and writes such a cookie with the leading dot and `TRUE`
  (`get_netscape_format`). The cases' `<file>` sections pin the result.

### UTC-2: an empty SASL EXTERNAL response is sent as an empty line, not `=`

- **Cases:** test838 "IMAP external authentication without credentials", test884 "POP3 external
  authentication without credentials", test943 "SMTP external authentication without credentials".
- **Command lines:** `curl 'imap://;AUTH=EXTERNAL@127.0.0.1:<port>/838/;MAILINDEX=1'`;
  `curl 'pop3://;AUTH=EXTERNAL@127.0.0.1:<port>/884'`;
  `curl 'smtp://;AUTH=EXTERNAL@127.0.0.1:<port>/943' --mail-rcpt recipient@example.com --mail-from sender@example.com -T -`.
- **Upstream:** after `AUTHENTICATE EXTERNAL` (IMAP) or `AUTH EXTERNAL` (POP3, SMTP) and the server's
  empty challenge, it sends the line `=`, then carries on and exits 0; the `<protocol>` sections pin
  `=` as the third line.
- **Port:** sends an empty line where upstream sends `=` (POP3 and SMTP: `<protocol>` line 3 is `\r\n`
  where `=\r\n` is expected). Over IMAP it does not finish: the case's server half answers only `=`
  with `A002 OK` (its `<servercmd>` is `REPLY = A002 OK AUTHENTICATE completed`), and the port was
  stopped after the case's 60-second limit.
- **What is right:** RFC 4422 section 4 and the profiles (RFC 4954 section 4, RFC 5034 section 4,
  RFC 4959 section 3) let a client send an empty initial response as `=`. Upstream's
  `lib/curl_sasl.c` at 8.21.0 sends EXTERNAL with no user name as a message of `=` (`build_message`
  with an empty buffer), and the three cases pin it.

### UTC-3: `--libcurl` writes its options in another order

- **Cases:** test1402 "--libcurl for simple POST", test1404 "--libcurl plus -F with 3 files…", test1405
  "--libcurl for FTP with quote ops", test1406 "--libcurl for SMTP", test1407 "--libcurl for POP3 LIST
  one message", test1465 "--libcurl for POST with binary content".
- **Command lines:** each case's `<command>` with `--libcurl log/test<N>.c`, for example
  `curl http://127.0.0.1:<port>/we/want/1402 --libcurl log/test1402.c -d "foo=bar" -d "baz=quux"` and
  `curl pop3://127.0.0.1:<port>/1407 -l -u user:secret --libcurl log/test1407.c`.
- **Upstream:** writes the `curl_easy_setopt` calls in the order the case's `<file>` gives, the order
  upstream's tool sets them at 8.21.0. For the port, the sections the checker reads
  before `file` (`stdout`, `protocol`, the reply's `data`, `upload`) pass: the transfer itself agrees.
- **Port:** writes the same calls in another order. First differing line of `log/test<N>.c`:
  - test1402 line 16: `curl_easy_setopt(curl, CURLOPT_USERAGENT, "curl/8.22.0");` where
    `curl_easy_setopt(curl, CURLOPT_POSTFIELDS, "foo=bar&baz=quux");` is expected;
  - test1404 line 27: `CURLOPT_USERAGENT` where `mime1 = curl_mime_init(curl);` is expected;
  - test1405 line 28: `curl_easy_setopt(curl, CURLOPT_QUOTE, slist1);` where
    `curl_easy_setopt(curl, CURLOPT_FTP_SKIP_PASV_IP, 1L);` is expected;
  - test1406 line 21: `CURLOPT_USERAGENT` where `curl_easy_setopt(curl, CURLOPT_UPLOAD, 1L);` is expected;
  - test1407 line 16: `curl_easy_setopt(curl, CURLOPT_USERPWD, "user:secret");` where
    `curl_easy_setopt(curl, CURLOPT_DIRLISTONLY, 1L);` is expected;
  - test1465 line 16: `CURLOPT_USERAGENT` where the `CURLOPT_POSTFIELDS` binary string is expected.
- **What is right:** the generated program is upstream's documented output of `--libcurl`
  (`docs/cmdline-opts/libcurl.md` at 8.21.0: "the source code for a C program using libcurl that does
  the equivalent"), and the cases' `<file>` sections (`tests/data/data1402.c` and the rest) pin its
  exact text, option order included.

### UTC-4: active-mode FTP with `-P` and a slow server does not finish

- **Case:** test251 "FTP dir list, PORT with specified IP and slow response" (new at port commit `74f3f45`).
- **Command line:** `curl ftp://127.0.0.1:<port>/ -P 127.0.0.1` against the case's FTP server half with `SLOWDOWN`.
- **Upstream:** logs in, sends `PWD`, `PORT`, `TYPE A`, `LIST`, `QUIT` and exits 0; the `<protocol>` section
  (with the `PORT` line stripped) passes.
- **Port:** did not finish: the run was stopped after the case's 60-second limit, with no section judged.
  The same result came on every run of the shift (three of three).
- **What is right:** `-P`/`--ftp-port` makes the client use active mode with the given address (RFC 959
  section 3.2, `PORT`); the case pins the command order and that the transfer completes.
- **Timing:** a timing-dependent finding (ADR-0089 amendment 10): the case is judged either way. Showing the
  timeout is this finding; finishing is neither stale nor a failure. Upstream must pass it every run.

## osx-arm64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed with the `Phase 7 families (osx-arm64)` job run. Its `phase-7-osx-arm64-results` artifact holds 489 results for all Phase 7 test classes.

- **This family:** 0 passed, 1 failed, 0 not executed.
- **Detail:** Failed: `RunBesideAsync_EveryCaseOnThePassingList_PortDefectsAreTheListedFindings`.
- **Cases compared.** The `UpstreamCurlBuilds.json` pin for `osx-arm64` (curl 8.21.0, SHA-256 `04E0E69B...EEBF`, pinned 2026-09-28) was found, and BL-1622 removed `UnpinnedCurlPortClientException` (357 of them in run 44044472038), so cases that reach the port compared it with upstream curl. An earlier statement that no upstream curl build is pinned for `osx-arm64` was false.
- **Triaged by BL-1625.** The one failure is the unlisted `test1072` and `test1073` (port exit 65, upstream 25), now listed under `### Findings (osx-arm64)` as UTC-5; the other 12 port-defect cases of the run were already listed. The Curl port is never an oracle (ADR-0003).

## linux-x64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed. Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing. It supersedes run 44044472038 and the later scheduled run 45198867449 (2026-11-23).

- **This family:** 1 passed, 0 failed, 0 not executed.
- **Detail:** Every case passed.
- **Triaged by BL-1626.** Nothing failed, so nothing is added: the listed UTC-4 (`test251`) and UTC-5 (`test1072`, `test1073`) still showed. One case flipped between runs: `test251` showed its timeout in runs 44044472038 and 45412760425 but was reported stale in run 45198867449 (it finished that time). The case is a 60-second timing race with a deliberately slow server, which no ADR-0089 amendment covers, so it stays listed; BL-1647 decided it is tolerated either way (ADR-0089 amendment 10).
