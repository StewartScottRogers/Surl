# ADR-0156 — What the FTP test-case half sends for `NODATACONN425`, `NODATACONN421`, `NODATACONN150`, `PASVBADIP` and `RETRWEIRDO`

- **Status:** Accepted
- **Date:** 2026-11-14
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-14,
  in BL-1135 (FR-053). Built by the task that follows it.
- **Amends:** [ADR-0080](ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md)
  decision 2 for the `ftp` half: five more `<servercmd>` words are implemented. The
  `FtpUpstreamTestCaseServerCommands` remarks, which list them as not implemented, change in the build.

## Context

Five FTP `<servercmd>` words keep upstream tests 1206, 1207, 1208, 270 and 126 skipped
(`servercmd-unsupported:<command>`) on every platform. FILEFORMAT.md at tag `curl-8_21_0` describes
`PASVBADIP` and `RETRWEIRDO` in one line each and does not mention the three `NODATACONN` words. The
evidence, in the order ADR-0003 and ADR-0080 set, is each case's `<verify>`, RFC 959, and
`tests/ftpserver.pl` at the tag **read only to learn what upstream's server means** by each word
(line numbers below are that file at `curl-8_21_0`; nothing is copied, and the Curl port is not
evidence). Every byte below is the half's own choice, justified by those; no new measurement was
needed because each case's `<protocol>` and exit code already pin what curl 8.21.0 does.

**The cases** (all `ftp`; the first three use `--max-time %FTPTIME2 ... -P -`):

| Case | Word | Command line | Exit code |
|---|---|---|---|
| 1206 | `NODATACONN425` | `-P -` (active, `EPRT`), RETR | 10 (`CURLE_FTP_ACCEPT_FAILED`) |
| 1207 | `NODATACONN421` | same | 10 |
| 1208 | `NODATACONN150` | same | 28 (`CURLE_OPERATION_TIMEDOUT`) |
| 270 | `PASVBADIP` | `--ftp-skip-pasv-ip --disable-epsv`, RETR | 0 |
| 126 | `RETRWEIRDO` | RETR over `EPSV` in `/blalbla/lululul/126` | 0 |

## Decision

**1. The three `NODATACONN` words share one rule: no data connection is ever made, and a data
command answers on the control connection instead.** Upstream (`ftpserver.pl` 2894 to 2910, 2476 to
2482, 2683 to 2688) sets `nodataconn` alongside the word and then, for `PORT`/`EPRT`, never starts the
data side, and for `PASV`/`EPSV` binds a port without listening (2552 to 2556, 2580 to 2584). The
half does the same: on `PORT`/`EPRT` it records the address and never connects out; on `PASV`/`EPSV`
it answers `227`/`229` as normal with a port it has reserved from the listener seam and never
accepts on (the client's connect may succeed into the backlog or fail; either leaves curl waiting for
the control reply). The reply to the `PORT`, `EPRT`, `PASV` or `EPSV` is unchanged, so the
`<protocol>` lists in 1206 to 1208 (`EPRT |1|`, `TYPE I`, `SIZE`, `RETR`) still match.

**2. What `RETR` (and `LIST`, `NLST`, `STOR`, the four commands upstream gives the same rule at
2093, 2141, 2264, 2380) sends when there is no data connection** (RFC 959 section 4.2: `150` file
status okay, about to open the data connection; `425` can't open data connection; `421` service not
available, closing the control connection):

| Word | Bytes written to the control connection | After |
|---|---|---|
| `NODATACONN425` | `150 Opening data connection\r\n` then `425 Can't open data connection\r\n` | control stays open; curl sends `QUIT` |
| `NODATACONN421` | `150 Opening data connection\r\n` then `421 Connection timed out\r\n` | control stays open; curl sends `QUIT` |
| `NODATACONN150` | `150 Opening data connection\r\n` only | nothing more is ever sent; control stays open until curl gives up |

Evidence: 1206 and 1207 expect exit 10 and a `QUIT` as the last control line; 1208 expects exit 28 and
**no** `QUIT` (its own comment: curl's timeout leaves it unable to assume the control connection is
usable). So the half must not close the control connection after the `421` (curl answers with `QUIT`
and the half answers that as any `QUIT`), and must send nothing after the lone `150`. The case's
`<data>` is never sent. The word applies to every later data command until the connection ends, as
upstream's per-connection flag does.

**3. `NODATACONN` alone** (no suffix) is not in these five cases and stays unimplemented:
upstream's reply for it is nothing at all, so it can be added when a case needs it
(`servercmd-unsupported:NODATACONN` stays its skip reason). The three suffixed words are matched by
exact first word, not by substring as upstream's regular expressions do (2894 to 2915).

**4. `PASVBADIP`: `227` names `1,2,3,4` as the host and the half's real data port.** Upstream
(`ftpserver.pl` 2566 to 2570) replaces the listen address with `1,2,3,4` in the `227` reply only. The
half sends `227 Entering Passive Mode (1,2,3,4,<p1>,<p2>)\r\n`, still opens the data listener and
still accepts normally; `EPSV` (`229 Entering Passive Mode (|||<port>|)`) is untouched, since it
carries no address. Test 270 passes `--ftp-skip-pasv-ip --disable-epsv`, so curl 8.21.0 ignores the
host in the `227` and connects to the control connection's own host: exit 0, the data arrives, and
the `<protocol>` is `PASV`, `TYPE I`, `SIZE`, `RETR`, `QUIT`.

**5. `RETRWEIRDO`: the `150` and the `226` reach curl in one write before any data, once.**
Upstream (`ftpserver.pl` 2338 to 2347) sends `150 Binary data connection for <testno> () (<size>
bytes).\r\n` and `226 File transfer complete\r\n` together, then the data on the data connection,
closes it, and clears the flag, so only the **first** `RETR` is weird. The half does the same: one
write of both lines (a client reading the control connection sees two replies in one segment), then
the data, then the data connection closes; no further `226`. The `150`'s test-part field is empty,
`()`, as upstream's is, and the size is the file's, as in the ordinary `RETR`. Case 126 expects exit 0
and the file's bytes, then `QUIT` as the last control line.

**6. The build adds these members** to `FtpUpstreamTestCaseServerCommands`: `NODATACONN425`,
`NODATACONN421` and `NODATACONN150` set one property, `NoDataConnectionReply` (an enum `None`,
`Refuse425`, `Timeout421`, `OpenOnly150`); `PASVBADIP` sets `ReportsBadPassiveAddress`; `RETRWEIRDO`
sets `SendsRetrieveRepliesTogether`. They join `Supported`, and the class's remarks and
`UpstreamTestCaseServers` stop listing them as unsupported. Each has a unit test replaying the bytes
above; the five cases move from skipped to run in `Upstream test cases`.

## Consequences

- 1206, 1207, 1208, 270 and 126 stop being skipped on Windows, Linux and macOS.
- 1208 is marked `flaky` and `timing-dependent` upstream; its 28 depends on `--max-time` elapsing
  against a half that sends nothing more, so the half never closes the control connection on its own.
- The five words are per-connection, never global state, so many instances in one test suite do not
  influence each other.
