---
id: BL-396
title: Play the server half of upstream dict, gopher, mqtt, rtsp, telnet and tftp test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-393]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary, Protocols/Lookup/Surl.Protocol.Dict.UnitTests, Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary, Protocols/Lookup/Surl.Protocol.Gopher.UnitTests, Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary, Protocols/Http/Surl.Protocol.Rtsp.UnitTests, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary, Protocols/Terminal/Surl.Protocol.Telnet.UnitTests, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-396 — Play the server half of upstream dict, gopher, mqtt, rtsp, telnet and tftp test cases

## Goal

The upstream test-case run covers cases whose `<server>` is `dict`, `gopher`, `gophers`, `mqtt`,
`rtsp`, `telnet` or `tftp`. Surl plays their server half as BL-386's ADR defines, and the pass-rate file
counts them.

## Context

- Decision: BL-386's ADR, decision 2, for these servers. `touches` names the Conformance projects and
  these protocol libraries. Follow the ADR if it places the code elsewhere.
- FILEFORMAT.md (tag `curl-8_21_0`): the TFTP `<servercmd>` `writedelay` (line 416), and the HTTP
  `rtp:` instruction that RTSP cases use (lines 397 to 414). The `<reply>` sections apply to every
  server.
- How Surl answers: ADR-0011 (DICT), ADR-0012 (Gopher), ADR-0013 (TFTP), ADR-0014 (MQTT), ADR-0025
  (TELNET), and ADR-0074 (RTSP; on macOS the pinned build has no `rtsp`, ADR-0026). The runner,
  checker and pass-rate file are BL-392 and BL-393's.

## Acceptance criteria

- [x] Unit tests drive each server half with an inline case. Each excluded instruction gives its named
      skip reason.
- [x] The run covers every runnable case for these servers on Windows. The pass-rate file's rows
      include them, and Notes record the counts per server.
- [x] Failing cases are grouped by cause into new tasks filed with `task-planner`, with their IDs in
      Notes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps
      100% line and branch coverage.

## Notes

- **Where the code went** (ADR-0080 decision 2, inside `touches`; no new ADR - every choice below is
  implementation detail inside decision 2, decided by Claude under Stewart's delegation):
  - `Surl.Protocol.Gopher`: `GopherUpstreamTestCaseServer` reads the selector with
    `GopherProtocolServer.ReadSelectorLineAsync` (now internal) and answers with the case's `<data>`;
    `GopherUpstreamTestCaseReplies`, `ReceivedBytesRecordingConnection`.
  - `Surl.Protocol.Mqtt`: `MqttUpstreamTestCaseServer` is `MqttProtocolServer` on a bus per
    connection; `MqttPacketLoggingConnection` + `MqttPacketCutter` log every packet in mqttd's form
    (`client <TYPE> <remlen hex> <remaining hex>`, the whole packet when it has none; `server <TYPE>
    <remlen hex> <whole packet hex>`) and publish `<data>`, retained, to each `SUBSCRIBE` topic just
    before Surl's server reads it, so Surl's own answer delivers it.
  - `Surl.Protocol.Tftp`: `TftpUpstreamTestCaseServer` logs each request in tftpd's form (`opcode`,
    `mode`, options in request order, `filename`) and answers with `TftpProtocolServer` over an
    in-memory store of its own holding `<data>`/`<dataNUM>` (the name's last digit run; over 10000
    picks NUM = remainder) at the requested path; a write is allowed only to a name carrying a
    digit (so `/invalid-file` gets Surl's ERROR 2, as test 1007 expects); `writedelay:` is
    `WriteDelayingDatagramFlow` (delay before each DATA packet, on the exchange's clock).
  - DICT and TELNET: the suite composes Surl's stock `DictProtocolServer` (empty in-memory store) and
    `TelnetProtocolServer`: test 1450's and 1452's `<data>` are empty, so there is nothing for a
    case seam to supply; neither library changed.
  - RTSP: all 10 `rtsp` cases at the tag (567-572, 577, 689, 3100) are libtests and skip `libtest`,
    so no RTSP server half is composed and `Surl.Protocol.Rtsp` did not change.
  - `Surl.Conformance`: `UpstreamTestCaseServerCommands.Tftp` (`Testnum`, `writedelay:`) and
    `None` (gopher, gopher-ipv6, gophers, mqtt, mqtts, dict, telnet). Tests:
    `UpstreamTestCaseServerHalves` composes every new listener (TFTP on a datagram listener);
    `UpstreamTestCasesAgainstSurlTests` maps each server name to its half, sets
    `%GOPHERPORT`/`%GOPHER6PORT`/`%GOPHERSPORT`/`%MQTTPORT`/`%MQTTSPORT`/`%DICTPORT`/`%TELNETPORT`/`%TFTPPORT`,
    gives `<servercmd>` to the first server's half only, joins `<protocol>` logs in the order the
    case names its servers, and `<upload>` from FTP and TFTP.
- **Excluded instructions** (each skipped `servercmd-unsupported:<command>`): MQTT `error-CONNACK`
  (1196, 2200, 2202, 2203), `remlen-CONNACK` (1132), `PUBLISH-before-SUBACK` (1194, 1195),
  `PINGRESP-as-CONNACK` (2206), `DISCONNECT-malformed` (2207), `excessive-remaining` (3017). Each makes
  the server send a deliberately broken or refusing packet; not implemented here.
- **Flake found and fixed:** the record was taken the moment curl exited, sometimes before the server
  half read curl's last `DISCONNECT` (1191, 1193 alternated). The run now waits up to 2 s for every
  open exchange to end (`UpstreamTestCaseServerHalves.WaitForExchangesToEndAsync`). Total run time
  is unchanged (about 90 s).
- **Counts**, Windows, reference build `C:\Program Files\Git\mingw64\bin\curl.exe` (SHA-256
  0E7737...8778), 2026-11-05, three runs, the last after the flake fix: of 2,013 cases **923 ran: 854
  pass, 69 fail; 1,090 skip** (was 888 / 829). New servers: **TFTP** 15 ran, 13 pass (+ 2002, 2003,
  HTTP+FTP+TFTP, fail); **MQTT** 11 ran (10 mqtt + 1640 mqtts), 6 pass; **Gopher** 5 ran, 5 pass;
  **DICT** 1 ran, 1 pass; **TELNET** 1 ran, 0 pass; **RTSP** 0 ran (all libtest). The passing list
  gains 25 (854 lines) and the pass-rate file's `win-x64` row says 92.5%.
- **Failures, grouped and filed:**
  - **BL-855**: TFTP netascii reads are sent without LF->CRLF conversion (1094), a Surl defect
    against RFC 1350.
  - **BL-856**: test-server choices - MQTT `PUBLISH` RETAIN bit (1190, 1192, 2204, 3018, 1640;
    Surl follows MQTT 3.1.1 section 3.3.1.3) and TFTP missing file ERROR 1 vs upstream's ERROR 2
    (283); depends on BL-689.
  - **BL-857**: a multi-server case's protocol log is joined per server, not by arrival (2002, 2003).
  - **BL-858**: decide how the TELNET half plays 1452 (Surl's server waits for `quit`; timeout).
- **Coverage** (`Measure-CodeQuality -Library ...`): Tftp, Gopher and Mqtt libraries 100% line, 100%
  branch, 0 failing members. `Surl.Conformance.UnitLibrary` 100% line, 100% branch; its 9 members
  over complexity 10 predate this task (BL-394's Notes list them).
- `dotnet format --verify-no-changes` reports only `ENDOFLINE` on files in this checkout, untouched
  ones included (the worktree's line endings), and nothing else.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl plays the server half of upstream dict, gopher, gophers, mqtt, mqtts, telnet and tftp cases (rtsp's are all libtest); Windows run 923 ran, 854 pass; failures filed as BL-855 to BL-858
