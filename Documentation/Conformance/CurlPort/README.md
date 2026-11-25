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

- **Platforms:** three, each with its own section in every family file (ADR-0089 decision 8 and
  Amendments 8 and 9):
  - `win-x64`, measured 2026-11-05 and 2026-11-06 and re-measured at the port commit below on
    2026-11-16;
  - `linux-x64`, in run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425)
    of `curl-port-phase-7` (2026-11-24, Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, BL-1624,
    triaged by BL-1626);
  - `osx-arm64`, in the same run (triaged by BL-1625).

  History: a first osx-arm64 run (run 44000208980, BL-1324) compared nothing, because the port was then
  pinned for `win-x64` alone; `CurlPortBuilds.json` now pins the port at one commit for all three
  platforms (table below).
- **Port pins** (`CurlPortBuilds.json`, port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, published
  2026-11-16): `win-x64` `curl.exe` `7F4ED055…FE467`, `linux-x64` `curl`
  `DF507F32E8B3E5F0444F89544A62C18C4CFA96C838AF8C98D74B9B044F8CF57A`, `osx-arm64` `curl`
  `B5B48DC7CB11F42F41BD7F45E127ADF08E6BD1A0D3CAA0F80840E1971046BBEE`.
- **Upstream builds** (`UpstreamCurlBuilds.json`): `win-x64` as listed below; `linux-x64` the static
  OpenSSL build `153CA463957609117D21A848BE29B70691B85F9E5CC9370C7DAA037B839A4E45`, the OpenLDAP build
  `4418AD0292EB222B746096021817FE99E646E1506DA034A2607A264C21C39480` (the `OpenLdap_` cases) and the
  OpenSSL/nghttp3 build `0B84717479708914C0EAFE39ECDB18CFEDB4DEACFE657863E8306A18AA8004AA`; `osx-arm64` the
  reference build `04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF`
  (`curl 8.21.0 (aarch64-apple-darwin) libcurl/8.21.0 OpenSSL/4.0.1 …`), which lists no `ldap` or `rtsp`.
- **Curl port:** commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, the one commit every family file
  measured (ADR-0089 decision 10):
  - `curl.exe` SHA-256 `7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467`, whose
    `--version` says `curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel`;
  - `Libcurl.dll` SHA-256 `FA53A0C83925E7A3326721E8C0243CE888E815AD40E37883DF78A151230AB953`
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
    is paired with the `OpenLdap_` cases, which ran on `linux-x64` in run 45412760425 (47 cases, all as
    listed) and are not measurable on `osx-arm64` (ADR-0089 Amendment 9).
- **Surl:** each family was measured at its own commit, named in its file and in the table below.
- **Dates:** 2026-11-05 and 2026-11-06; the extended clients' cases up to 2026-11-11.

## Results (win-x64)

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
| [Upstream test cases](Upstream-Test-Cases.md) | `7bf55f26` | 1,185 | 1,171 | 12 | 4 | 2 |
| [Extended clients](Extended.md) | `477dda7d` and the others its tasks name | 21 | 20 | 1 | 6 | 0 |
| **All** | | **1,662** | **1,563** | **97** | **24** | **2** |

Not counted above (win-x64): cases Inconclusive on Windows because their mirrored tests run only elsewhere (4
HTTP cases, the 47 `OpenLdap_` cases) or are excluded on Windows by their `[OSCondition]` (6 SSH cases,
3 FTP listing twins).

## Results (linux-x64 and osx-arm64)

Run 45412760425 (2026-11-24, port commit `74f3f45c…`). Each cell is "passed / failed / not executed" as the
run reported it, before triage; every failure is triaged in its family file (BL-1625 for osx-arm64, BL-1626
for linux-x64) into a findings row, a stale listing removed, or a test defect fixed, and none showed a Surl
defect. A dash is a family the run's artifact has no result for.

| Family file | linux-x64 | osx-arm64 |
| --- | --- | --- |
| [HTTP/1.1 and HTTPS](Http.md) | 35 / 1 / 21 | 35 / 1 / 21 |
| [FTP and FTPS](Ftp.md) | 39 / 4 / 3 | 43 / 0 / 3 |
| [SCP and SFTP](Ssh.md) | 71 / 1 / 7 | 70 / 2 / 7 |
| [SMTP, IMAP and POP3](Mail.md) | 108 / 13 / 0 | 121 / 0 / 0 |
| [DICT, Gopher, TELNET, TFTP and MQTT](Dict-Gopher-Telnet-Tftp-Mqtt.md) | 26 / 0 / 0 | 26 / 0 / 0 |
| [WebSocket](WebSocket.md) | 20 / 0 / 0 | 20 / 0 / 0 |
| [LDAP and LDAPS](Ldap.md) | 47 / 0 / 22 | 0 / 0 / 69 |
| [SMB and SMBS](Smb.md) | 24 / 0 / 0 | 24 / 0 / 0 |
| [RTSP](Rtsp.md) | 16 / 0 / 0 | 0 / 0 / 16 |
| [HTTP/2](Http2.md) | 5 / 10 / 0 | 5 / 10 / 0 |
| [HTTP/3](Http3.md) | 12 / 1 / 0 | 12 / 1 / 0 |
| [libcurl rows](Libcurl.md) | - | - |
| [Upstream test cases](Upstream-Test-Cases.md) | 1 / 0 / 0 | 0 / 1 / 0 |
| [Extended clients](Extended.md) | - | - |

What triage left: HTTP/2 finding 2 (a Digest 401 reset on prior knowledge) on both platforms; LDAP L-1 to
L-4 on linux-x64; SSH-3 (blowfish-cbc and cast128-cbc, where libssh2 on macOS crashes) on osx-arm64; UTC-5
(`test1072`, `test1073`) on both and UTC-4 (`test251`) on linux-x64. Together with the win-x64 findings
they are the handoff's 31 findings.

**Not measurable** (ADR-0089 Amendment 9): the `OpenLdap_` cases and the RTSP family on `osx-arm64`
(the reference build lists no `ldap` or `rtsp`); the `WinLdap_` cases, the libcurl rows and the
`[OSCondition(Windows)]` cases on both; the extended clients are outside Phase 7. Phase 7's closing
condition for a platform holds in all four parts on both platforms (the pin exists, every failure is a
row, part 2 holds for every measured family, and each family file carries the literal line "not
measurable on <platform>: <reason>" for its not-measurable rows, added by BL-1648).

Two pairs of findings share a root and are worth fixing together: HTTP/2's finding 1 and HTTP/3's
finding 2 (no `h2` offered by ALPN), and HTTP's finding 1 with SSH's SSH-1 (the port offers the WinCNG
backend's SSH algorithms whatever it runs on). Each stays its own entry in the handoff, because each is
its own row in a findings table its test reads.

The extended clients are measured by the extended track's own `Integration` tasks, not beside upstream
curl (ADR-0089 decision 9), and each case is judged by its protocol's specification. All 19 extended
`Integration` tasks are `Done`; their six port defects (EXT-1 to EXT-6) are in [`Extended.md`](Extended.md).

## The handoff

[`Port-Board-Handoff.md`](Port-Board-Handoff.md) holds the 29 entries (31 findings) as tasks in the port's own task
shape, with the port task ID of each. All are filed on the port's board (BL-1658, 2026-11-24), by running
the port's `/task-plan` on the handoff in a session of the port's repository (ADR-0089 decision 7). No
Surl session writes into the port's repository. SSH-3 (entry 28) is an accepted difference by the port's
side (port BL-5909); upstream curl's verdict on its two cases is unchanged.
