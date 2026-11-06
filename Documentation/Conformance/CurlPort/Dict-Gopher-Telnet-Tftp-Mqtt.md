# Phase 7 findings: DICT, Gopher, TELNET, TFTP and MQTT

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
| Curl port | commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build, paired for every scheme here) |
| Surl | commit `15a2e03bcff75fbed29d156032d3fb634c59d205` |
| Platform | Windows (`win-x64`); off Windows every case is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-05 |

## Result

36 cases in 26 tests on Windows: 36 agreements, no port defect, no Surl defect, none not comparable.
Upstream's run is what the mirrored test asserts in every case. The run was repeated twice with the same
result.

| Family | Tests | Cases | Request bytes compared |
| --- | --- | --- | --- |
| DICT | 1 (5 data rows) | 5 | Yes, every `dict://` connection |
| Gopher | 3 | 3 | Yes, every `gopher://` connection; the mirrored class runs no `gophers://` conversation |
| TELNET | 3 | 3 | No (ADR-0089 amendment 2) |
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

What decision 4 compares here, and what amendment 2 changed to compare it truly:

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
