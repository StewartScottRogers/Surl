---
id: BL-1138
title: Decide what the MQTT server half does for the seven mqttd server commands upstream cases use
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1138 — Decide what the MQTT server half does for the seven mqttd server commands upstream cases use

## Goal

An ADR, decided by Claude under Stewart's delegation, states for each MQTT server command upstream
cases use - `error-CONNACK <code>`, `remlen-CONNACK <n>`, `PINGRESP-as-CONNACK`,
`PUBLISH-before-SUBACK`, `short-PUBLISH`, `DISCONNECT-malformed` and `excessive-remaining` - the exact
packet bytes the MQTT upstream test-case server half sends and when, so the two build tasks need no
further choice.

## Context

- These keep 10 upstream tests skipped (`servercmd-unsupported:<command>`) on Windows, Linux and
  macOS (CI run 43629075978; local Windows reference run 2026-11-14): 1196, 2200, 2202, 2203
  (`error-CONNACK 1` or `5`), 1132 (`remlen-CONNACK 3`), 2206 (`PINGRESP-as-CONNACK TRUE`), 1194
  (`PUBLISH-before-SUBACK TRUE`), 1195 (that and `short-PUBLISH TRUE`), 2207
  (`DISCONNECT-malformed TRUE`) and 3017 (`excessive-remaining TRUE`).
- `docs/tests/FILEFORMAT.md` at tag `curl-8_21_0` documents none of them; they belong to upstream's
  MQTT test server, `tests/server/mqttd.c`.
- Evidence, in this order (ADR-0003; ADR-0080: written from specifications, curl's documentation and
  measurement, not by translating upstream's test servers):
  1. each case's `<verify>` (`<protocol>`, `<errorcode>`, `<stdout>`);
  2. MQTT 3.1.1 (OASIS, 2014), the version curl's `mqtt://` speaks, for each packet's layout;
  3. `tests/server/mqttd.c` at the tag, read only to learn what each word is meant to make the server
     do, never copied;
  4. a measurement with a pinned build through `Record-CurlExchange.ps1` if a byte is still open.
- Today `MqttUpstreamTestCaseReplies` (`Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary`) takes
  only the `<data>` bytes and `SupportedServerCommands` is empty; `UpstreamTestCaseServerCommands`
  maps `mqtt` to `None`. The ADR says how the half receives the `<servercmd>` lines.
- Check the exclusions file's MQTT rows (1190, 1192, 2204, 3018, 1640: RETAIN on a new
  subscription's `PUBLISH`) and say whether 1194 and 1195 meet the same byte.
- Next free ADR number at filing: ADR-0155, or ADR-0156 if BL-1135 lands first.

## Acceptance criteria

- [ ] An ADR under `Documentation/Planning/Decisions/` marked "Decided by Claude under Stewart's
      delegation" has one decision per command: the packet bytes (hex), when they are sent, and what
      the half does with the connection after.
- [ ] Each decision names its evidence (case section, MQTT 3.1.1 section, `mqttd.c` line read at tag
      `curl-8_21_0`, or a recorded measurement) and the curl version; none cites the Curl port.
- [ ] The ADR lists the cases each decision serves and the exit code each expects, and says how the
      half receives the `<servercmd>` lines.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.

## Notes

- Decided in ADR-0155. Every byte comes from the cases' own `<protocol>` sections and MQTT 3.1.1; `mqttd.c` was not
  read (not needed) and no measurement was made. 1194, 1195, 2207 and 3017 expect `30 0c` (RETAIN clear), so the half
  writes that `PUBLISH` itself; they meet no exclusion.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
