# Phase 7: the Curl port measured beside pinned upstream curl

Phase 7 runs the Curl port (https://github.com/StewartScottRogers/Curl) through the same conversations
as pinned upstream curl, each client against its own fresh `surl`, and judges every difference by
upstream curl (ADR-0003 decision 6;
[ADR-0089](../../Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md)).
A disagreement is the port's defect. It is filed on the port's own board through
[`Port-Board-Handoff.md`](Port-Board-Handoff.md), and Surl does not change to make the port pass.

This file sums up the findings files in this folder. Each family file is the record for its family:
its Phase 7 test reads the file's findings table, so the file stays true of the code (ADR-0089
decision 5).

## What was measured

- **Platform:** Windows (`win-x64`) only. `CurlPortBuilds.json` pins the port for `win-x64` alone, so
  off Windows every Phase 7 test is Inconclusive (ADR-0089 decision 8).
- **Curl port:** commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, the one commit every family file
  measured (ADR-0089 decision 10):
  - `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`, whose
    `--version` says `curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel`;
  - `Libcurl.dll` SHA-256 `613976A7CC3DA746AB74819C6A526D676A39998D56B5470D5C527A902D7C8311`
    (kind `library`, the libcurl rows only).
- **Upstream curl** (`UpstreamCurlBuilds.json`), each case paired with the build its mirrored test runs
  (ADR-0089 decision 2):
  - the Windows reference build, SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`,
    `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel … libssh2/1.11.1 WinLDAP`;
  - the static OpenSSL build, SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`,
    `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 … nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0`
    (HTTP/2, HTTP/3, SMB, and the cases whose mirrored tests run it);
  - the reference build's `libcurl-4.dll`, SHA-256 `799F7EEFC3C9DA9C80EC5AEA221A02B3AFE2C5350C6B45FD5A4865E7E2D4E574`
    (the libcurl rows);
  - the OpenLDAP `linux-x64` build, SHA-256 `4418AD0292EB222B746096021817FE99E646E1506DA034A2607A264C21C39480`,
    is paired with the `OpenLdap_` cases, which stay Inconclusive until a `linux-x64` port pin exists.
- **Surl:** each family was measured at its own commit, named in its file and in the table below.
- **Dates:** 2026-11-05 and 2026-11-06.

## Results

A case is one run pair: a test, or one data row of a data-driven test. "Port defects" counts cases;
"Findings" counts the distinct defects behind them, each one entry in the handoff. No case showed a
Surl defect: upstream's run was what the mirrored test asserts in every case.

| Family file | Surl commit | Cases | Agreements | Port defects | Findings | Not comparable |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| [HTTP/1.1 and HTTPS](Http.md) | `489f8d6f` | 53 | 43 | 10 | 4 | 0 |
| [FTP and FTPS](Ftp.md) | `ea263ee6` | 43 | 43 | 0 | 0 | 0 |
| [SCP and SFTP](Ssh.md) | `4c501095` | 73 | 68 | 5 | 1 | 0 |
| [SMTP, IMAP and POP3](Mail.md) | `8f7deb30` | 121 | 121 | 0 | 0 | 0 |
| [DICT, Gopher, TELNET, TFTP and MQTT](Dict-Gopher-Telnet-Tftp-Mqtt.md) | `15a2e03b` | 36 | 36 | 0 | 0 | 0 |
| [WebSocket](WebSocket.md) | `c17373cb` | 20 | 20 | 0 | 0 | 0 |
| [LDAP and LDAPS](Ldap.md) | `00351846` | 22 | 3 | 19 | 3 | 0 |
| [SMB and SMBS](Smb.md) | `4e626fa8` | 24 | 0 | 24 | 1 | 0 |
| [RTSP](Rtsp.md) | `fdb0ab43` | 16 | 13 | 3 | 1 | 0 |
| [HTTP/2](Http2.md) | `6268bee5` | 15 | 6 | 9 | 1 | 0 |
| [HTTP/3](Http3.md) | `883c3d4d` | 13 | 0 | 13 | 2 | 0 |
| [libcurl rows](Libcurl.md) | `dc8aebe3`, `2006b077` | 20 | 19 | 1 | 1 | 0 |
| [Upstream test cases](Upstream-Test-Cases.md) | `7bf55f26` | 1,041 | 1,028 | 11 | 3 | 2 |
| [Extended clients](Extended.md) | none yet | 0 | 0 | 0 | 0 | 0 |
| **All** | | **1,497** | **1,400** | **95** | **17** | **2** |

Not counted above: cases Inconclusive on Windows because their mirrored tests run only elsewhere (4
HTTP cases, the 47 `OpenLdap_` cases) or are excluded on Windows by their `[OSCondition]` (6 SSH cases,
3 FTP listing twins).

Two pairs of findings share a root and are worth fixing together: HTTP/2's finding 1 and HTTP/3's
finding 2 (no `h2` offered by ALPN), and HTTP's finding 1 with SSH's SSH-1 (the port offers the WinCNG
backend's SSH algorithms whatever it runs on). Each stays its own entry in the handoff, because each is
its own row in a findings table its test reads.

The extended clients are measured by the extended track's own `Integration` tasks, not beside upstream
curl (ADR-0089 decision 9). None of those tasks was `Done` when this summary was written, so
[`Extended.md`](Extended.md) holds no finding yet and lists each task still to run.

## The handoff

[`Port-Board-Handoff.md`](Port-Board-Handoff.md) holds the 17 findings as tasks in the port's own task
shape. Stewart files them on the port's board himself, by running the port's `/task-plan` on it in a
session of the port's repository (ADR-0089 decision 7). No Surl session writes into the port's
repository.
