# Phase 7 findings: upstream curl's own test cases

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
| Curl port | commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`, `win-x64`); its `--version` says `curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel` |
| Upstream curl | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build, paired with every case but the smb ones) |
| Upstream curl, smb | SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0` (the supplementary build `LocateForProtocol` pairs with `smb`) |
| Surl | commit `7bf55f26a8eecda5f936db7e32f596770ee7adf5` with BL-591's changes |
| Platform | Windows (`win-x64`); off Windows the test is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-06 |

## Result

1,041 cases on Windows: upstream passed all 1,041 (no Surl defect), the port passed 1,028, 11 are port
defects and 2 are not comparable. The run was repeated with the same result.

| Family | Cases | Upstream passed | Port passed | Port defects | Not comparable |
| --- | ---: | ---: | ---: | ---: | ---: |
| dict | 1 | 1 | 1 | 0 | 0 |
| ftp | 171 | 171 | 170 | 1 | 0 |
| gopher | 5 | 5 | 5 | 0 | 0 |
| http | 658 | 658 | 653 | 5 | 0 |
| mail (imap, pop3, smtp) | 146 | 146 | 141 | 5 | 0 |
| mqtt | 6 | 6 | 6 | 0 | 0 |
| smb | 2 | 2 | 0 | 0 | 2 |
| ssh (scp, sftp) | 38 | 38 | 38 | 0 | 0 |
| tftp | 14 | 14 | 14 | 0 | 0 |
| **All** | **1,041** | **1,041** | **1,028** | **11** | **2** |

A case's family is its first `<server>`'s. The win-x64 passing list holds no telnet case.

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
