# ADR-0155 — What the MQTT test-case half sends for the seven mqttd server commands

- **Status:** Accepted
- **Date:** 2026-11-14
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-14, in BL-1138.
- **Amends:** [ADR-0080](ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md) decision 2 (the MQTT half implements no `<servercmd>`).

## Context

Ten upstream cases (curl 8.21.0, tag `curl-8_21_0`) are skipped `servercmd-unsupported:<command>` because
`MqttUpstreamTestCaseReplies.SupportedServerCommands` is empty and `UpstreamTestCaseServerCommands` maps `mqtt` to
`None`. Evidence for every byte below is the case's own `<verify><protocol>` and `<errorcode>` (ADR-0003, ADR-0080),
which is the exact log the half must produce, plus MQTT 3.1.1 (OASIS, 2014) for the packet layouts. `tests/server/mqttd.c`
was not consulted for this ADR (none was needed: each protocol section fixes the bytes); no byte comes from the Curl port.

Packet log lines are `client|server <NAME> <remaining length hex> <hex>`; the half writes them. Three logged names
are not packet-type names (`PINGRESP-as-CONNACK`, `DISCONNECT-malformed`), so the half logs those packets itself.

## Decisions

### 0. How the half receives the lines

`MqttUpstreamTestCaseReplies` takes a second constructor argument, `IReadOnlyList<string> serverCommands` (the case's
`<servercmd>` lines, as `FtpUpstreamTestCaseReplies` takes them), parsed by a new `MqttUpstreamTestCaseServerCommands`.
A line whose first word is not supported throws `NotSupportedException`. `SupportedServerCommands` becomes
`Testnum`, `error-CONNACK`, `remlen-CONNACK`, `PINGRESP-as-CONNACK`, `PUBLISH-before-SUBACK`, `short-PUBLISH`,
`DISCONNECT-malformed`, `excessive-remaining`; `UpstreamTestCaseServerCommands` gains an `Mqtt` row with the same words and
`UpstreamTestCaseServerCommandsFor` maps `mqtt` (and `mqtts`) to it. A value of `TRUE` is the only one upstream's cases give a
flag; any other value is `NotSupportedException`. Without a command the half behaves as today.

### 1. `error-CONNACK <code>` (cases 1196 code 1, 2200, 2202, 2203 code 5; curl exit 8)

After the client's `CONNECT` (whatever its credentials) the half writes `20 02 00 <code>` - `2002 0001` or `2002 0005` -
logs `server CONNACK 2 <hex>`, and closes. Nothing after: curl stops at the refusal. MQTT 3.1.1 3.2.2.3 (return code 1
"unacceptable protocol version", 5 "not authorized"). The `<code>` is the line's decimal argument, 1 to 5.

### 2. `remlen-CONNACK <n>` (case 1132, `3`; curl exit 8)

After `CONNECT` the half writes the four bytes `20 <n> 00 00` (`20030000`), logs `server CONNACK 3 20030000` (the
header's remaining length, not the bytes present), and closes. A real `CONNACK` has remaining length 2 (3.2.1); curl
rejects the 3 on reading the header, so no further byte is sent.

### 3. `PINGRESP-as-CONNACK TRUE` (case 2206; curl exit 8)

After `CONNECT` the half writes `d0 02 00 00` in place of the `CONNACK` (a `PINGRESP` must have remaining length 0,
3.13.1), logs `server PINGRESP-as-CONNACK 2 d0020000`, and closes.

### 4. `PUBLISH-before-SUBACK TRUE` (case 1194; curl succeeds, prints `hello`)

`CONNECT` is answered as normal (`20020000`). On `SUBSCRIBE` the half writes, as one answer and in this order,
`PUBLISH` `30 <len> 00 <topic length> <topic> <data>` (case 1194: `300c0004 31313934 68656c6c6f0a`), `SUBACK`
`90 03 <id> 00` (`9003000100`), then `DISCONNECT` `e0 00`; it logs each as `server PUBLISH`, `SUBACK`, `DISCONNECT` in that
order and closes. The topic is the first filter of the `SUBSCRIBE`, the payload the case's `<data>`, flags `0x30` (RETAIN
clear, QoS 0), written by the half and not by Surl's `MqttProtocolServer`.

### 5. `short-PUBLISH TRUE` (case 1195; with 4; curl exit 18)

As 4, but the `PUBLISH` is cut: header and remaining length are unchanged (`30 0c`) and the last two bytes of the
payload are omitted (`300c00043131393568656c6c`, 12 of 14 bytes), logged as `server PUBLISH c 300c00043131393568656c6c`
(the declared remaining length). The half then sends no `SUBACK` and no `DISCONNECT`, and closes, so curl sees a truncated
packet (`CURLE_PARTIAL_FILE`, 18).

### 6. `DISCONNECT-malformed TRUE` (case 2207; curl exit 8)

`CONNECT` as normal; on `SUBSCRIBE` the half writes `SUBACK` (`9003000100`), then `PUBLISH` (`300c0004 32323037
68656c6c6f0a`, flags `0x30`), then `e0 02 00 00` in place of `DISCONNECT` (3.14.1 requires remaining length 0), logged
`server DISCONNECT-malformed 2 e0020000`, and closes. Note the order is `SUBACK` first, as in case 1190.

### 7. `excessive-remaining TRUE` (case 3017; curl exit 8, run with `-m 3`)

`CONNECT` as normal; on `SUBSCRIBE` the half writes `SUBACK` (`9003000100`), then a `PUBLISH` whose fixed header is
`30 ff ff ff 80` - a remaining length with the continuation bit set on its fourth byte, which 3.3.1/2.2.3 forbid (at most
four bytes, the last without it) - followed by the 12 bytes `00043330313768656c6c6f0a`, and logs it
`server PUBLISH c 30ffffff8000043330313768656c6c6f0a` (`c` is the byte count after the header, because the packet cutter
cannot cut such a header and the half logs it itself). Then it sends `e0 00` and closes, or just closes: the case strips the
`server DISCONNECT 0 e000` line, so either passes; the half sends `e0 00` as upstream's other cases do.

### 8. Relation to the RETAIN exclusions (1190, 1192, 2204, 3018, 1640)

Those cases stay excluded: Surl's own server sets RETAIN (`31 0c`) on the `PUBLISH` of a new subscription as MQTT 3.1.1
3.3.1.3 says. Cases 1194, 1195, 2207 and 3017 expect `30 0c` (RETAIN clear) as well, and meet no exclusion because
decisions 4 to 7 have the half itself write that `PUBLISH` and the `SUBACK` and `DISCONNECT` round it; they never go
through `MqttProtocolServer`'s `PUBLISH`. Cases 4 to 7 are therefore not added to `Upstream-Test-Case-Exclusions.md`.

## Consequences

Two build tasks follow: the half's parsing and writing (`Surl.Protocol.Mqtt.UnitLibrary`, with unit tests that replay the
byte strings above), and the conformance wiring (`UpstreamTestCaseServerCommands`, the skip reasons, the pass-rate
document). A command that is `servercmd-unsupported` today passes there: 1196, 2200, 2202, 2203, 1132, 2206, 1194, 1195,
2207, 3017. Measurement against a pinned build was not needed as every byte is in a case; a build task that finds a case
failing records the measured byte in an amendment.
